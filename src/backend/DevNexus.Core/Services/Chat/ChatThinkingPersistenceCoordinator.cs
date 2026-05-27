using DevNexus.Shared.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 聊天思维链持久化协调器。
/// 统一处理局部 thinking/text 持久化、外部 thinking 合并，以及最终 thinking 回写。
/// </summary>
public sealed class ChatThinkingPersistenceCoordinator
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly ILogger<ChatThinkingPersistenceCoordinator> _logger;

    public ChatThinkingPersistenceCoordinator(
        IServiceScopeFactory serviceScopeFactory,
        IChatMessageRepository chatMessageRepository,
        ILogger<ChatThinkingPersistenceCoordinator> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _chatMessageRepository = chatMessageRepository;
        _logger = logger;
    }

    public async Task PersistPartialThinkingAsync(
        Guid sessionId,
        Guid messageId,
        string partialThinking)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var chatMessageRepository = scope.ServiceProvider.GetRequiredService<IChatMessageRepository>();

            var message = await chatMessageRepository.GetByIdAsync(messageId, CancellationToken.None);
            if (message == null)
            {
                return;
            }

            if (!message.Content.ContainsKey(ChatMessageContentKeys.ThinkingPartial))
            {
                message.Content[ChatMessageContentKeys.ThinkingPartial] = string.Empty;
            }

            var existing = message.Content[ChatMessageContentKeys.ThinkingPartial]?.ToString() ?? string.Empty;
            message.Content[ChatMessageContentKeys.ThinkingPartial] = existing + partialThinking;
            var persisted = message.Content[ChatMessageContentKeys.ThinkingPartial]?.ToString() ?? string.Empty;

            await chatMessageRepository.UpdateAsync(message, CancellationToken.None);

            _logger.LogDebug(
                "[Thinking.Trace] PersistPartial | Source={Source} SessionId={SessionId} MessageId={MessageId} DeltaLength={DeltaLength} PreviousLength={PreviousLength} PersistedLength={PersistedLength} DeltaHash={DeltaHash} Preview={Preview}",
                "InternalPeriodic",
                sessionId,
                messageId,
                partialThinking.Length,
                existing.Length,
                persisted.Length,
                ThinkingTraceHelper.ComputeHash(partialThinking),
                ThinkingTraceHelper.CreatePreview(partialThinking));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[Persistence] Failed to persist partial thinking | SessionId={SessionId} MessageId={MessageId}",
                sessionId,
                messageId);
        }
    }

    public async Task PersistPartialTextAsync(
        Guid sessionId,
        Guid messageId,
        string partialText)
    {
        if (string.IsNullOrEmpty(partialText))
        {
            return;
        }

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var chatMessageRepository = scope.ServiceProvider.GetRequiredService<IChatMessageRepository>();

            var message = await chatMessageRepository.GetByIdAsync(messageId, CancellationToken.None);
            if (message == null)
            {
                return;
            }

            if (!message.Content.ContainsKey(ChatMessageContentKeys.TextPartial))
            {
                message.Content[ChatMessageContentKeys.TextPartial] = string.Empty;
            }

            var existing = message.Content[ChatMessageContentKeys.TextPartial]?.ToString() ?? string.Empty;
            message.Content[ChatMessageContentKeys.TextPartial] = existing + partialText;

            await chatMessageRepository.UpdateAsync(message, CancellationToken.None);

            _logger.LogDebug(
                "[Persistence] Persisted partial text | SessionId={SessionId} MessageId={MessageId} Length={Length}",
                sessionId,
                messageId,
                partialText.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[Persistence] Failed to persist partial text | SessionId={SessionId} MessageId={MessageId}",
                sessionId,
                messageId);
        }
    }

    public async Task<string> MergeExternalThinkingAsync(
        Guid messageId,
        string currentThinking,
        CancellationToken cancellationToken = default)
    {
        return await ThinkingPersistenceHelper.MergeExternalThinkingAsync(
            _chatMessageRepository,
            messageId,
            currentThinking,
            _logger,
            cancellationToken);
    }

    public void ApplyFinalThinking(
        ChatMessage message,
        string finalThinking)
    {
        if (!string.IsNullOrEmpty(finalThinking))
        {
            MergePartialThinking(message, finalThinking);
        }
        else if (message.Content.ContainsKey(ChatMessageContentKeys.ThinkingPartial))
        {
            message.Content[ChatMessageContentKeys.Thinking] = message.Content[ChatMessageContentKeys.ThinkingPartial]?.ToString() ?? string.Empty;
            message.Content.Remove(ChatMessageContentKeys.ThinkingPartial);
        }

        if (message.Content.ContainsKey(ChatMessageContentKeys.ThinkingExternalPartial))
        {
            message.Content.Remove(ChatMessageContentKeys.ThinkingExternalPartial);
        }
    }

    public static string MergeThinkingForPersistence(
        StringBuilder? preParserThinking,
        string? parserThinking)
    {
        var preContent = preParserThinking?.ToString() ?? string.Empty;
        var finalContent = parserThinking ?? string.Empty;
        return string.Concat(preContent, finalContent);
    }

    public static void LogThinkingFinalAssemble(
        ILogger logger,
        Guid sessionId,
        Guid messageId,
        string preThinking,
        string parserThinking,
        string contextThinking)
    {
        logger.LogDebug(
            "[Thinking.Trace] FinalAssemble | SessionId={SessionId} MessageId={MessageId} PreLength={PreLength} PreHash={PreHash} ParserLength={ParserLength} ParserHash={ParserHash} ContextLength={ContextLength} ContextHash={ContextHash}",
            sessionId,
            messageId,
            preThinking.Length,
            ThinkingTraceHelper.ComputeHash(preThinking),
            parserThinking.Length,
            ThinkingTraceHelper.ComputeHash(parserThinking),
            contextThinking.Length,
            ThinkingTraceHelper.ComputeHash(contextThinking));
    }

    private static string MergeThinking(string partialThinking, string finalThinking)
    {
        return string.Concat(partialThinking, finalThinking);
    }

    private void MergePartialThinking(ChatMessage aiMessage, string finalThinking)
    {
        if (aiMessage.Content.ContainsKey(ChatMessageContentKeys.ThinkingPartial))
        {
            var partialThinking = aiMessage.Content[ChatMessageContentKeys.ThinkingPartial]?.ToString() ?? string.Empty;
            var merged = MergeThinking(partialThinking, finalThinking);
            aiMessage.Content[ChatMessageContentKeys.Thinking] = merged;
            aiMessage.Content.Remove(ChatMessageContentKeys.ThinkingPartial);

            _logger.LogDebug(
                "[Thinking.Trace] FinalMerge | Source={Source} MessageId={MessageId} PartialLength={PartialLength} PartialHash={PartialHash} FinalLength={FinalLength} FinalHash={FinalHash} MergedLength={MergedLength} MergedHash={MergedHash}",
                "InternalPeriodic",
                aiMessage.Id,
                partialThinking.Length,
                ThinkingTraceHelper.ComputeHash(partialThinking),
                finalThinking.Length,
                ThinkingTraceHelper.ComputeHash(finalThinking),
                merged.Length,
                ThinkingTraceHelper.ComputeHash(merged));
        }
        else
        {
            aiMessage.Content[ChatMessageContentKeys.Thinking] = finalThinking;

            _logger.LogDebug(
                "[Thinking.Trace] FinalMerge | Source={Source} MessageId={MessageId} PartialLength=0 FinalLength={FinalLength} FinalHash={FinalHash} MergedLength={MergedLength} MergedHash={MergedHash}",
                "DirectFinal",
                aiMessage.Id,
                finalThinking.Length,
                ThinkingTraceHelper.ComputeHash(finalThinking),
                finalThinking.Length,
                ThinkingTraceHelper.ComputeHash(finalThinking));
        }
    }
}
