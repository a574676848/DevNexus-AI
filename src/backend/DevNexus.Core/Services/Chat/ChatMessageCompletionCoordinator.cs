using DevNexus.Core.Abstractions.Observability;
using DevNexus.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 聊天消息完成后协调器。
/// 负责统一处理索引同步、记忆沉淀与完成日志。
/// </summary>
public interface IChatMessageCompletionCoordinator
{
    /// <summary>
    /// 处理消息完成后的统一后置动作。
    /// </summary>
    Task HandleCompletedAsync(
        ChatSession chatSession,
        ChatMessage aiMessage,
        Guid userId,
        int agentLoopAttempt,
        int responseLength,
        bool includeExperienceDistillation = true,
        SelfIterationCandidateDecision? selfIterationCandidate = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 聊天消息完成后协调器实现。
/// </summary>
internal sealed class ChatMessageCompletionCoordinator : IChatMessageCompletionCoordinator
{
    private readonly ChatSearchService _chatSearchService;
    private readonly IBackgroundJobService _backgroundJobService;
    private readonly IDistributedTracingService _tracingService;
    private readonly ILogger<ChatMessageCompletionCoordinator> _logger;

    public ChatMessageCompletionCoordinator(
        ChatSearchService chatSearchService,
        IBackgroundJobService backgroundJobService,
        IDistributedTracingService tracingService,
        ILogger<ChatMessageCompletionCoordinator> logger)
    {
        _chatSearchService = chatSearchService;
        _backgroundJobService = backgroundJobService;
        _tracingService = tracingService;
        _logger = logger;
    }

    public async Task HandleCompletedAsync(
        ChatSession chatSession,
        ChatMessage aiMessage,
        Guid userId,
        int agentLoopAttempt,
        int responseLength,
        bool includeExperienceDistillation = true,
        SelfIterationCandidateDecision? selfIterationCandidate = null,
        CancellationToken cancellationToken = default)
    {
        await _chatSearchService.SyncSessionToElasticsearchAsync(chatSession, cancellationToken);
        await _chatSearchService.SyncMessageToElasticsearchAsync(aiMessage, chatSession.UserId, cancellationToken);

        if (includeExperienceDistillation)
        {
            _backgroundJobService.ScheduleExperienceDistillation(
                chatSession.Id,
                TimeSpan.FromMinutes(2),
                ExperienceDistillationScheduleContext.Create(
                    selfIterationCandidate?.Reason,
                    selfIterationCandidate?.ContextPressureReason,
                    selfIterationCandidate?.ContextCompressionSummaryFingerprint));
        }

        await _tracingService.LogStructuredEventAsync(
            TraceEvent.MessageGenerationCompleted,
            "Information",
            $"消息生成完成 | SessionId={chatSession.Id} MessageId={aiMessage.Id} ContentLength={responseLength} AttemptNumber={agentLoopAttempt}");

        _logger.LogDebug(
            "[AI.Chat] Streaming completed | SessionId={SessionId} MessageId={MessageId} Length={Length}",
            chatSession.Id,
            aiMessage.Id,
            responseLength);
    }
}
