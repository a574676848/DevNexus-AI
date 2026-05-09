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

            if (!message.Content.ContainsKey("thinking_partial"))
            {
                message.Content["thinking_partial"] = string.Empty;
            }

            var existing = message.Content["thinking_partial"]?.ToString() ?? string.Empty;
            message.Content["thinking_partial"] = existing + partialThinking;
            var persisted = message.Content["thinking_partial"]?.ToString() ?? string.Empty;

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

            if (!message.Content.ContainsKey("text_partial"))
            {
                message.Content["text_partial"] = string.Empty;
            }

            var existing = message.Content["text_partial"]?.ToString() ?? string.Empty;
            message.Content["text_partial"] = existing + partialText;

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
        else if (message.Content.ContainsKey("thinking_partial"))
        {
            message.Content["thinking"] = message.Content["thinking_partial"]?.ToString() ?? string.Empty;
            message.Content.Remove("thinking_partial");
        }

        if (message.Content.ContainsKey("thinking_external_partial"))
        {
            message.Content.Remove("thinking_external_partial");
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

    private void MergePartialThinking(ChatMessage aiMessage, string finalThinking)
    {
        if (aiMessage.Content.ContainsKey("thinking_partial"))
        {
            var partialThinking = aiMessage.Content["thinking_partial"]?.ToString() ?? string.Empty;
            var merged = partialThinking + finalThinking;
            aiMessage.Content["thinking"] = merged;
            aiMessage.Content.Remove("thinking_partial");

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
            aiMessage.Content["thinking"] = finalThinking;

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
