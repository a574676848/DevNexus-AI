using DevNexus.Core.Abstractions;
using DevNexus.Domain.Abstractions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 会话统一运行时服务。
/// 负责从 PendingInteraction、CLI、排队和消息状态聚合出当前会话的统一运行态。
/// </summary>
public interface IChatSessionRuntimeService
{
    /// <summary>
    /// 获取指定会话的运行时快照。
    /// </summary>
    Task<ChatSessionRuntimeDto> GetRuntimeAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 会话统一运行时服务实现。
/// </summary>
internal sealed class ChatSessionRuntimeService : IChatSessionRuntimeService
{
    private readonly IQueuedChatMessageRepository _queuedChatMessageRepository;
    private readonly IChatSessionRuntimeInspector _runtimeInspector;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public ChatSessionRuntimeService(
        IQueuedChatMessageRepository queuedChatMessageRepository,
        IChatSessionRuntimeInspector runtimeInspector)
    {
        _queuedChatMessageRepository = queuedChatMessageRepository;
        _runtimeInspector = runtimeInspector;
    }

    /// <inheritdoc />
    public async Task<ChatSessionRuntimeDto> GetRuntimeAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var queuedCount = await _queuedChatMessageRepository.CountPendingBySessionAsync(sessionId, cancellationToken);
        var runtime = await _runtimeInspector.InspectAsync(userId, sessionId, queuedCount, cancellationToken);

        return new ChatSessionRuntimeDto
        {
            SessionId = sessionId,
            RunState = runtime.RunState,
            PendingInteractionCount = runtime.PendingInteractionCount,
            PrimaryPendingInteractionKind = runtime.PrimaryPendingInteractionKind,
            PrimaryPendingInteractionId = runtime.PrimaryPendingInteractionId,
            PrimaryPendingInteractionTitle = runtime.PrimaryPendingInteractionTitle,
            PrimaryPendingInteractionDescription = runtime.PrimaryPendingInteractionDescription,
            QueuedCount = runtime.QueuedCount,
            HasActiveCliSession = runtime.HasActiveCliSession,
            CliWaitingForInput = runtime.CliWaitingForInput,
            HasInProgressAssistantMessage = runtime.HasInProgressAssistantMessage
        };
    }
}
