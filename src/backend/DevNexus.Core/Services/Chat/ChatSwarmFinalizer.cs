using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Threading.Channels;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Swarm 编排收尾协调器。
/// 统一处理成功、取消、失败三种收尾路径，减少 ChatService.Swarm 重复代码。
/// </summary>
public sealed class ChatSwarmFinalizer
{
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly ChatThinkingPersistenceCoordinator _thinkingCoordinator;
    private readonly IChatMessageCompletionCoordinator _messageCompletionCoordinator;
    private readonly ISwarmEventService _swarmEventService;
    private readonly ILogger<ChatSwarmFinalizer> _logger;

    public ChatSwarmFinalizer(
        IChatMessageRepository chatMessageRepository,
        ChatThinkingPersistenceCoordinator thinkingCoordinator,
        IChatMessageCompletionCoordinator messageCompletionCoordinator,
        ISwarmEventService swarmEventService,
        ILogger<ChatSwarmFinalizer> logger)
    {
        _chatMessageRepository = chatMessageRepository;
        _thinkingCoordinator = thinkingCoordinator;
        _messageCompletionCoordinator = messageCompletionCoordinator;
        _swarmEventService = swarmEventService;
        _logger = logger;
    }

    public async Task FinalizeCompletedAsync(
        ChatMessage aiMessage,
        ChatSession chatSession,
        string swarmResult,
        bool isTruncated,
        string thinkingContent,
        ChannelWriter<BlockDto> blockWriter,
        CancellationToken cancellationToken = default)
    {
        aiMessage.Content = new Dictionary<string, object>
        {
            { "text", swarmResult }
        };

        _thinkingCoordinator.ApplyFinalThinking(aiMessage, thinkingContent);

        aiMessage.Status = isTruncated ? ChatConstants.StatusTruncated : ChatConstants.StatusCompleted;
        aiMessage.UpdatedAt = DateTime.UtcNow;
        aiMessage.Metadata ??= new Dictionary<string, object>();
        aiMessage.Metadata[ChatMessageMetadataKeys.SwarmMode] = true;

        await _chatMessageRepository.UpdateAsync(aiMessage, cancellationToken);
        await _messageCompletionCoordinator.HandleCompletedAsync(
            chatSession,
            aiMessage,
            chatSession.UserId,
            agentLoopAttempt: 0,
            responseLength: swarmResult.Length,
            includeExperienceDistillation: false,
            cancellationToken);

        await _swarmEventService.NotifySwarmCompletedAsync(
            chatSession.Id.ToString(),
            swarmResult.Length,
            cancellationToken);

        await blockWriter.WriteAsync(new BlockDto
        {
            BlockType = BlockType.TextDelta,
            Content = string.Empty,
            MessageId = aiMessage.Id,
            SessionId = chatSession.Id,
            IsLast = false,
            Metadata = new Dictionary<string, object> { ["swarmEvent"] = SwarmEventNames.Completed }
        }, cancellationToken);

        _logger.LogInformation(
            "[AI.Swarm] Swarm orchestration completed | SessionId={SessionId} ResultLength={Length}",
            chatSession.Id,
            swarmResult.Length);
    }

    public async Task FinalizeCancelledAsync(
        ChatMessage aiMessage,
        ChatSession chatSession,
        string thinkingContent,
        ChannelWriter<BlockDto> blockWriter,
        CancellationToken cancellationToken = default)
    {
        aiMessage.Content = new Dictionary<string, object>
        {
            { "text", "Swarm 执行被用户中止（已部分生成）" }
        };

        _thinkingCoordinator.ApplyFinalThinking(aiMessage, thinkingContent);

        aiMessage.Status = ChatConstants.StatusCancelled;
        aiMessage.UpdatedAt = DateTime.UtcNow;
        aiMessage.Metadata ??= new Dictionary<string, object>();
        aiMessage.Metadata[ChatMessageMetadataKeys.SwarmMode] = true;

        await _chatMessageRepository.UpdateAsync(aiMessage, cancellationToken);

        await _swarmEventService.NotifySwarmCancelledAsync(
            chatSession.Id.ToString(),
            "Cancelled by user",
            cancellationToken);

        await blockWriter.WriteAsync(new BlockDto
        {
            BlockType = BlockType.TextDelta,
            Content = string.Empty,
            MessageId = aiMessage.Id,
            SessionId = chatSession.Id,
            IsLast = false,
            Metadata = new Dictionary<string, object> { ["swarmEvent"] = SwarmEventNames.Failed }
        }, cancellationToken);
    }

    public async Task FinalizeFailedAsync(
        ChatMessage aiMessage,
        ChatSession chatSession,
        string errorDetails,
        ChannelWriter<BlockDto> blockWriter,
        CancellationToken cancellationToken = default)
    {
        await blockWriter.WriteAsync(new BlockDto
        {
            BlockType = BlockType.Warning,
            Content = $"Swarm 编排执行失败：{errorDetails}",
            MessageId = aiMessage.Id,
            SessionId = chatSession.Id,
            Metadata = new Dictionary<string, object>
            {
                [FeedbackBlockMetadataConstants.Level] = FeedbackBlockMetadataConstants.LevelError,
                [FeedbackBlockMetadataConstants.Source] = FeedbackBlockMetadataConstants.SourceChatServiceSwarm,
                ["swarmEvent"] = SwarmEventNames.Failed
            }
        }, cancellationToken);

        await blockWriter.WriteAsync(new BlockDto
        {
            BlockType = BlockType.TextDelta,
            Content = string.Empty,
            MessageId = aiMessage.Id,
            SessionId = chatSession.Id,
            IsLast = true
        }, cancellationToken);

        aiMessage.Status = ChatConstants.StatusError;
        aiMessage.Content = new Dictionary<string, object>
        {
            { "text", $"Swarm 编排执行失败：{errorDetails}" }
        };
        aiMessage.UpdatedAt = DateTime.UtcNow;
        aiMessage.Metadata ??= new Dictionary<string, object>();
        aiMessage.Metadata[ChatMessageMetadataKeys.SwarmMode] = true;

        await _chatMessageRepository.UpdateAsync(aiMessage, cancellationToken);

        await _swarmEventService.NotifySwarmFailedAsync(
            chatSession.Id.ToString(),
            errorDetails,
            cancellationToken);
    }
}
