using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using DevNexus.Shared.Constants;
using System.Text;

namespace DevNexus.Core.Services.Chat;

public sealed class ChatHistoryMessageBuilder
{
    private const string InternalRepairPromptMetadataKey = "internalRepairPrompt";

    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly ISessionSummaryService _sessionSummaryService;
    private readonly ILogger<ChatHistoryMessageBuilder> _logger;

    public ChatHistoryMessageBuilder(
        IChatMessageRepository chatMessageRepository,
        ISessionSummaryService sessionSummaryService,
        ILogger<ChatHistoryMessageBuilder> logger)
    {
        _chatMessageRepository = chatMessageRepository;
        _sessionSummaryService = sessionSummaryService;
        _logger = logger;
    }

    /// <summary>
    /// 追加历史消息并返回上下文治理快照。
    /// </summary>
    public async Task<ChatHistoryGovernanceSnapshot> AppendHistoryMessagesAsync(
        ChatHistory chatHistory,
        Guid sessionId,
        Guid? providerId,
        int tokenBudget,
        CancellationToken cancellationToken)
    {
        if (tokenBudget <= 0)
        {
            _logger.LogWarning(
                "[AI.Chat] History budget exhausted before loading messages | SessionId={SessionId} Budget={Budget}",
                sessionId,
                tokenBudget);
            return new ChatHistoryGovernanceSnapshot
            {
                BudgetTokens = tokenBudget,
                Strategy = ChatHistoryGovernanceStrategies.BudgetExhausted
            };
        }

        const int maxMessagesToFetch = 50;
        const int recentMessagesToKeep = 10;

        var tokenThreshold = Math.Max(8000, tokenBudget / 2);
        var allMessages = (await _chatMessageRepository.ListRecentBySessionAsync(
            sessionId,
            maxMessagesToFetch,
            cancellationToken)).ToList();

        allMessages.Reverse();

        var skippedInternalRepairPrompts = allMessages.Count(IsInternalRepairPrompt);
        var skippedIncompleteAssistantMessages = allMessages
            .Where(message => !IsInternalRepairPrompt(message))
            .Count(message => !IsReplayableMessage(message));
        var validMessages = allMessages
            .Where(message => !IsInternalRepairPrompt(message))
            .Where(IsReplayableMessage)
            .Select(message => new ChatHistoryMessageEntry(
                message.SenderType,
                ChatHistoryReplayTextSanitizer.Clean(
                    message.Content.ContainsKey("text")
                        ? message.Content["text"].ToString()
                        : string.Empty)))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Content))
            .ToList();

        if (validMessages.Count == 0)
        {
            _logger.LogDebug("[AI.Chat] AI 聊天： No valid messages found | SessionId={SessionId}", sessionId);
            return new ChatHistoryGovernanceSnapshot
            {
                BudgetTokens = tokenBudget,
                FetchedMessageCount = allMessages.Count,
                SkippedInternalRepairPromptCount = skippedInternalRepairPrompts,
                SkippedIncompleteAssistantMessageCount = skippedIncompleteAssistantMessages
            };
        }

        var totalTokens = validMessages.Sum(entry => ChatHistoryTextHelper.EstimateTokenCount(entry.Content));
        var remainingBudget = tokenBudget;

        _logger.LogDebug(
            "[AI.Chat] Building chat history | SessionId={SessionId} MessageCount={Count} EstimatedTokens={Tokens} Budget={Budget}",
            sessionId,
            validMessages.Count,
            totalTokens,
            tokenBudget);

        var effectiveThreshold = Math.Min(tokenThreshold, tokenBudget);
        if (totalTokens <= effectiveThreshold || validMessages.Count <= recentMessagesToKeep)
        {
            var directResult = AppendDirectMessages(chatHistory, validMessages, sessionId, tokenBudget);
            return new ChatHistoryGovernanceSnapshot
            {
                BudgetTokens = tokenBudget,
                ConsumedTokens = directResult.AddedTokens,
                FetchedMessageCount = allMessages.Count,
                ReplayableMessageCount = validMessages.Count,
                DirectMessageCount = directResult.AddedCount,
                SkippedInternalRepairPromptCount = skippedInternalRepairPrompts,
                SkippedIncompleteAssistantMessageCount = skippedIncompleteAssistantMessages,
                TruncatedByBudget = directResult.Truncated,
                Strategy = ChatHistoryGovernanceStrategies.DirectReplay
            };
        }

        var recentMessages = ChatHistoryRecentSlicePolicy
            .Select(validMessages, recentMessagesToKeep)
            .ToList();
        var olderMessages = validMessages.Take(validMessages.Count - recentMessagesToKeep).ToList();

        var summaryResult = await AppendCompressedSummaryAsync(
            chatHistory,
            olderMessages,
            sessionId,
            providerId,
            remainingBudget,
            cancellationToken);
        remainingBudget = summaryResult.RemainingBudget;

        var recentResult = AppendRecentMessages(chatHistory, recentMessages, sessionId, remainingBudget, validMessages.Count);
        return new ChatHistoryGovernanceSnapshot
        {
            BudgetTokens = tokenBudget,
            ConsumedTokens = summaryResult.AddedTokens + recentResult.AddedTokens,
            FetchedMessageCount = allMessages.Count,
            ReplayableMessageCount = validMessages.Count,
            DirectMessageCount = recentResult.AddedCount,
            CompressedMessageCount = summaryResult.SummaryAdded ? olderMessages.Count : 0,
            SummaryMessageCount = summaryResult.SummaryAdded ? 1 : 0,
            RecentMessageCount = recentResult.AddedCount,
            CompressionIndex = summaryResult.CompressionIndex,
            SkippedInternalRepairPromptCount = skippedInternalRepairPrompts,
            SkippedIncompleteAssistantMessageCount = skippedIncompleteAssistantMessages,
            TruncatedByBudget = recentResult.Truncated,
            Strategy = ChatHistoryGovernanceStrategies.SummaryWithRecentSlice
        };
    }

    private AppendMessagesResult AppendDirectMessages(
        ChatHistory chatHistory,
        IReadOnlyList<ChatHistoryMessageEntry> validMessages,
        Guid sessionId,
        int tokenBudget)
    {
        var directAddedTokens = 0;
        var addedCount = 0;
        foreach (var item in validMessages)
        {
            var processedContent = item.Content.Length > 5000
                ? ChatHistoryTextHelper.TruncateOutput(item.Content, 4000)
                : item.Content;

            var msgTokens = ChatHistoryTextHelper.EstimateTokenCount(processedContent);
            if (directAddedTokens + msgTokens > tokenBudget)
            {
                _logger.LogWarning(
                    "[AI.Chat] Truncating messages due to total token limit | SessionId={SessionId} Budget={Budget}",
                    sessionId,
                    tokenBudget);
                break;
            }

            ChatHistoryTextHelper.AddMessageToChatHistory(chatHistory, item.SenderType, processedContent);
            directAddedTokens += msgTokens;
            addedCount++;
        }

        return new AppendMessagesResult(directAddedTokens, addedCount, addedCount < validMessages.Count);
    }

    private async Task<AppendSummaryResult> AppendCompressedSummaryAsync(
        ChatHistory chatHistory,
        IReadOnlyList<ChatHistoryMessageEntry> olderMessages,
        Guid sessionId,
        Guid? providerId,
        int remainingBudget,
        CancellationToken cancellationToken)
    {
        if (olderMessages.Count == 0 || !providerId.HasValue)
        {
            return new AppendSummaryResult(remainingBudget, 0, SummaryAdded: false);
        }

        var olderContent = new StringBuilder();
        olderContent.AppendLine("以下是对话的早期内容：");
        foreach (var item in olderMessages)
        {
            var role = ChatConstants.IsUserSender(item.SenderType) ? "用户" : "助手";
            olderContent.AppendLine($"[{role}]: {item.Content}");
        }

        try
        {
            var targetChars = Math.Max(500, olderContent.Length / 3);
            var summary = await _sessionSummaryService.GetOrCreateSummaryAsync(
                sessionId,
                providerId.Value,
                olderContent.ToString(),
                targetChars,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(summary))
            {
                return new AppendSummaryResult(remainingBudget, 0, SummaryAdded: false);
            }

            var summaryMessage = $"[对话历史摘要]\n以下是本次对话早期内容的摘要，供参考：\n{summary}";
            var summaryTokens = ChatHistoryTextHelper.EstimateTokenCount(summaryMessage);

            chatHistory.AddSystemMessage(summaryMessage);
            remainingBudget -= summaryTokens;
            var compressionIndex = ChatHistoryCompressionIndexBuilder.Build(olderMessages, summary);

            _logger.LogDebug(
                "[AI.Chat] Compressed {Count} older messages into summary | SessionId={SessionId} SummaryTokens={Tokens} TopicHints={TopicHints}",
                olderMessages.Count,
                sessionId,
                summaryTokens,
                compressionIndex.TopicHints.Count);
            return new AppendSummaryResult(remainingBudget, summaryTokens, SummaryAdded: true, compressionIndex);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[AI.Chat] Failed to generate summary for older messages, using truncation | SessionId={SessionId}",
                sessionId);
        }

        return new AppendSummaryResult(remainingBudget, 0, SummaryAdded: false);
    }

    private AppendMessagesResult AppendRecentMessages(
        ChatHistory chatHistory,
        IReadOnlyList<ChatHistoryMessageEntry> recentMessages,
        Guid sessionId,
        int remainingBudget,
        int totalMessageCount)
    {
        var addedTokens = 0;
        var addedCount = 0;

        foreach (var item in recentMessages)
        {
            var msgTokens = ChatHistoryTextHelper.EstimateTokenCount(item.Content);
            if (addedTokens + msgTokens > remainingBudget)
            {
                _logger.LogWarning(
                    "[AI.Chat] Truncating recent messages due to token limit | SessionId={SessionId} Added={Added} Skipped={Skipped}",
                    sessionId,
                    addedCount,
                    recentMessages.Count - addedCount);
                break;
            }

            ChatHistoryTextHelper.AddMessageToChatHistory(chatHistory, item.SenderType, item.Content);
            addedTokens += msgTokens;
            addedCount++;
        }

        _logger.LogDebug(
            "[AI.Chat] Built ChatHistory | SessionId={SessionId} TotalMessages={Total} AddedMessages={Added}",
            sessionId,
            totalMessageCount,
            addedCount);

        return new AppendMessagesResult(addedTokens, addedCount, addedCount < recentMessages.Count);
    }

    private static bool IsInternalRepairPrompt(ChatMessage message)
    {
        if (message.Metadata == null
            || !message.Metadata.TryGetValue(InternalRepairPromptMetadataKey, out var value)
            || value == null)
        {
            return false;
        }

        return bool.TryParse(value.ToString(), out var isInternal) && isInternal;
    }

    private static bool IsReplayableMessage(ChatMessage message)
    {
        return !ChatConstants.IsAssistantSender(message.SenderType)
            || ChatConstants.IsCompletedStatus(message.Status);
    }

    private readonly record struct AppendMessagesResult(
        int AddedTokens,
        int AddedCount,
        bool Truncated);

    private readonly record struct AppendSummaryResult(
        int RemainingBudget,
        int AddedTokens,
        bool SummaryAdded,
        ChatHistoryCompressionIndex CompressionIndex)
    {
        public AppendSummaryResult(int remainingBudget, int addedTokens, bool SummaryAdded)
            : this(remainingBudget, addedTokens, SummaryAdded, ChatHistoryCompressionIndex.Empty)
        {
        }
    }
}
