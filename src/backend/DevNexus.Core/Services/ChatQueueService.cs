using System.Text.Json;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Domain.Enums;
using DevNexus.Core.Services.Chat;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevNexus.Core.Services;

/// <summary>
/// 聊天消息排队服务实现。
/// 负责统一接管发送请求，根据执行状态决策立即发送、入队排队或转发给运行时。
/// </summary>
public class ChatQueueService : IChatQueueService
{
    private readonly IChatSessionRuntimeInspector _runtimeInspector;
    private readonly IQueuedChatMessageRepository _queueRepository;
    private readonly ILogger<ChatQueueService> _logger;

    public ChatQueueService(
        IChatSessionRuntimeInspector runtimeInspector,
        IQueuedChatMessageRepository queueRepository,
        ILogger<ChatQueueService> logger)
    {
        _runtimeInspector = runtimeInspector;
        _queueRepository = queueRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<EnqueueResult> HandleSendRequestAsync(
        Guid userId,
        Guid sessionId,
        string content,
        Guid? parentMessageId,
        string messageType = ChatConstants.MessageTypeText,
        string? selectedSkillName = null,
        IReadOnlyCollection<Guid>? artifactIds = null,
        Guid? llmProviderId = null,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        // 第一步：通过执行状态解析器决定消息流向
        var queuedCount = await _queueRepository.CountPendingBySessionAsync(sessionId, cancellationToken);
        var runtime = await _runtimeInspector.InspectAsync(userId, sessionId, queuedCount, cancellationToken);
        var decision = runtime.ExecutionDecision;

        switch (decision)
        {
            case ChatExecutionDecision.Immediate:
                // 空闲态或普通生成中，走立即发送链路
                _logger.LogInformation(
                    "[ChatQueue] 会话 {SessionId} 决策为立即发送 | UserId={UserId}",
                    sessionId, userId);
                return new EnqueueResult(
                    ChatExecutionDecision.Immediate,
                    Message: "立即发送");

            case ChatExecutionDecision.Queued:
                // 长作业占用态，消息入队
                return await EnqueueMessageAsync(
                    userId, sessionId, content, parentMessageId, messageType,
                    selectedSkillName, artifactIds, llmProviderId, metadata, cancellationToken);

            case ChatExecutionDecision.ForwardToRuntimeInput:
                // 等待输入态，输入应直达当前作业
                _logger.LogDebug(
                    "[ChatQueue] 会话 {SessionId} 处于等待输入态 → ForwardToRuntimeInput | UserId={UserId}",
                    sessionId, userId);
                return new EnqueueResult(
                    ChatExecutionDecision.ForwardToRuntimeInput,
                    Message: "当前作业正在等待输入，请直接输入内容");

            case ChatExecutionDecision.Rejected:
            default:
                var rejectedMessage = BuildRejectedMessage(runtime);
                _logger.LogWarning(
                    "[ChatQueue] 会话 {SessionId} 决策为拒绝 | UserId={UserId}",
                    sessionId, userId);
                return new EnqueueResult(
                    ChatExecutionDecision.Rejected,
                    Message: rejectedMessage);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QueuedChatMessageDto>> GetQueueAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var messages = await _queueRepository.ListBySessionAsync(sessionId, cancellationToken);
        return messages
            .Where(message => message.UserId == userId)
            .Select(MapToDto)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<bool> CancelQueuedMessageAsync(
        Guid sessionId,
        Guid queuedMessageId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var message = await _queueRepository.GetByIdAsync(queuedMessageId, cancellationToken);

        if (message == null)
        {
            _logger.LogWarning(
                "[ChatQueue] 尝试取消不存在的排队消息 | QueuedMessageId={QueuedMessageId}",
                queuedMessageId);
            return false;
        }

        if (message.UserId != userId || message.ChatSessionId != sessionId)
        {
            _logger.LogWarning(
                "[ChatQueue] 用户尝试取消不属于当前会话的排队消息 | UserId={UserId} SessionId={SessionId} QueuedMessageId={QueuedMessageId}",
                userId,
                sessionId,
                queuedMessageId);
            return false;
        }

        // 仅 Pending 状态的消息可以取消
        if (message.Status != QueuedMessageStatus.Pending)
        {
            _logger.LogWarning(
                "[ChatQueue] 尝试取消非 Pending 状态的排队消息（当前状态={Status}）| QueuedMessageId={QueuedMessageId}",
                message.Status, queuedMessageId);
            return false;
        }

        message.Status = QueuedMessageStatus.Cancelled;
        message.CancelledAt = DateTime.UtcNow;
        message.UpdatedAt = DateTime.UtcNow;

        await _queueRepository.UpdateAsync(message, cancellationToken);

        _logger.LogInformation(
            "[ChatQueue] 已取消排队消息 | QueuedMessageId={QueuedMessageId} SessionId={SessionId} UserId={UserId}",
            queuedMessageId, message.ChatSessionId, userId);

        return true;
    }

    /// <inheritdoc />
    public async Task<int> ClearQueueAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var cancelledCount = await _queueRepository.CancelAllPendingBySessionAsync(sessionId, cancellationToken);

        _logger.LogInformation(
            "[ChatQueue] 已清空会话 {SessionId} 的排队消息 | CancelledCount={Count}",
            sessionId, cancelledCount);

        return cancelledCount;
    }

    /// <inheritdoc />
    public async Task<int> GetPendingCountAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _queueRepository.CountPendingBySessionAsync(sessionId, cancellationToken);
    }

    /// <summary>
    /// 将消息加入排队队列。
    /// </summary>
    private async Task<EnqueueResult> EnqueueMessageAsync(
        Guid userId,
        Guid sessionId,
        string content,
        Guid? parentMessageId,
        string messageType,
        string? selectedSkillName,
        IReadOnlyCollection<Guid>? artifactIds,
        Guid? llmProviderId,
        Dictionary<string, object>? metadata,
        CancellationToken cancellationToken)
    {
        var queuedMessage = await CreateQueuedMessageWithRetryAsync(
            userId,
            sessionId,
            content,
            parentMessageId,
            messageType,
            selectedSkillName,
            artifactIds,
            llmProviderId,
            metadata,
            cancellationToken);

        var pendingCount = await _queueRepository.CountPendingBySessionAsync(sessionId, cancellationToken);

        _logger.LogInformation(
            "[ChatQueue] 消息已入队 | QueuedMessageId={QueuedMessageId} SessionId={SessionId} UserId={UserId} Seq={SequenceNumber} PendingCount={PendingCount}",
            queuedMessage.Id, sessionId, userId, queuedMessage.SequenceNumber, pendingCount);

        return new EnqueueResult(
            ChatExecutionDecision.Queued,
            queuedMessage.Id,
            pendingCount,
            $"已加入等待队列，当前作业结束后将自动发送（排队第 {pendingCount} 位）");
    }

    /// <summary>
    /// 为会话分配下一个可用队列序号。
    /// </summary>
    private async Task<QueuedChatMessage> CreateQueuedMessageWithRetryAsync(
        Guid userId,
        Guid sessionId,
        string content,
        Guid? parentMessageId,
        string messageType,
        string? selectedSkillName,
        IReadOnlyCollection<Guid>? artifactIds,
        Guid? llmProviderId,
        Dictionary<string, object>? metadata,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 5;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var nextSequenceNumber = await _queueRepository.GetMaxSequenceNumberAsync(sessionId, cancellationToken) + 1;
            var queuedMessage = new QueuedChatMessage
            {
                ChatSessionId = sessionId,
                UserId = userId,
                ParentMessageId = parentMessageId,
                Content = content,
                MessageType = ChatConstants.NormalizeMessageType(messageType),
                SelectedSkillName = selectedSkillName,
                LLMProviderId = llmProviderId,
                ArtifactIdsJson = artifactIds != null && artifactIds.Count > 0
                    ? JsonSerializer.Serialize(artifactIds)
                    : null,
                MetadataJson = metadata != null && metadata.Count > 0
                    ? JsonSerializer.Serialize(metadata)
                    : null,
                Status = QueuedMessageStatus.Pending,
                SequenceNumber = nextSequenceNumber
            };

            try
            {
                await _queueRepository.AddAsync(queuedMessage, cancellationToken);
                return queuedMessage;
            }
            catch (DbUpdateException) when (attempt < maxAttempts)
            {
                _logger.LogWarning(
                    "[ChatQueue] 队列序号写入冲突，准备重试 | SessionId={SessionId} Attempt={Attempt} SequenceNumber={SequenceNumber}",
                    sessionId,
                    attempt,
                    nextSequenceNumber);
            }
        }

        throw new InvalidOperationException("分配排队序号失败，请稍后重试。");
    }

    private static QueuedChatMessageDto MapToDto(QueuedChatMessage message)
    {
        return new QueuedChatMessageDto
        {
            Id = message.Id,
            SessionId = message.ChatSessionId,
            Content = message.Content,
            MessageType = ChatConstants.NormalizeMessageType(message.MessageType),
            SelectedSkillName = message.SelectedSkillName,
            Status = message.Status.ToString(),
            SequenceNumber = message.SequenceNumber,
            CreatedAt = message.CreatedAt,
            StartedAt = message.StartedAt,
            CompletedAt = message.CompletedAt,
            CancelledAt = message.CancelledAt,
            FailureReason = message.FailureReason,
            ActualMessageId = message.ActualMessageId
        };
    }

    private static string BuildRejectedMessage(ChatSessionRuntimeSnapshot runtime)
    {
        if (runtime.PendingInteractionCount <= 0)
        {
            return "当前无法处理发送请求，请稍后重试";
        }

        if (runtime.PrimaryPendingInteractionKind == PendingInteractionKind.Approval)
        {
            return string.IsNullOrWhiteSpace(runtime.PrimaryPendingInteractionDescription)
                ? "当前会话正在等待审批，审批完成后才能继续发送。"
                : $"当前会话正在等待审批：{runtime.PrimaryPendingInteractionDescription}";
        }

        return string.IsNullOrWhiteSpace(runtime.PrimaryPendingInteractionDescription)
            ? "当前会话正在等待补充信息，补充完成后才能继续发送。"
            : $"当前会话正在等待补充信息：{runtime.PrimaryPendingInteractionDescription}";
    }
}
