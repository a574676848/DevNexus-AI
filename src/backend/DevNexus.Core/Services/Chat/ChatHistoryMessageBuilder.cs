using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using DevNexus.Shared.Constants;
using System.Text;

namespace DevNexus.Core.Services.Chat;

public sealed class ChatHistoryMessageBuilder
{
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly ChatHistorySummaryService _summaryService;
    private readonly ILogger<ChatHistoryMessageBuilder> _logger;

    public ChatHistoryMessageBuilder(
        IChatMessageRepository chatMessageRepository,
        ChatHistorySummaryService summaryService,
        ILogger<ChatHistoryMessageBuilder> logger)
    {
        _chatMessageRepository = chatMessageRepository;
        _summaryService = summaryService;
        _logger = logger;
    }

    public async Task AppendHistoryMessagesAsync(
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
            return;
        }

        const int maxMessagesToFetch = 50;
        const int recentMessagesToKeep = 10;

        var tokenThreshold = Math.Max(8000, tokenBudget / 2);
        var allMessages = (await _chatMessageRepository.ListRecentBySessionAsync(
            sessionId,
            maxMessagesToFetch,
            cancellationToken)).ToList();

        allMessages.Reverse();

        var validMessages = allMessages
            .Select(message => new HistoryMessageEntry(
                message.SenderType,
                message.Content.ContainsKey("text")
                    ? message.Content["text"].ToString() ?? string.Empty
                    : string.Empty))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Content))
            .ToList();

        if (validMessages.Count == 0)
        {
            _logger.LogDebug("[AI.Chat] AI 聊天： No valid messages found | SessionId={SessionId}", sessionId);
            return;
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
            AppendDirectMessages(chatHistory, validMessages, sessionId, tokenBudget);
            return;
        }

        var recentMessages = validMessages.TakeLast(recentMessagesToKeep).ToList();
        var olderMessages = validMessages.Take(validMessages.Count - recentMessagesToKeep).ToList();

        remainingBudget = await AppendCompressedSummaryAsync(
            chatHistory,
            olderMessages,
            sessionId,
            providerId,
            remainingBudget,
            cancellationToken);

        AppendRecentMessages(chatHistory, recentMessages, sessionId, remainingBudget, validMessages.Count);
    }

    private void AppendDirectMessages(
        ChatHistory chatHistory,
        IReadOnlyList<HistoryMessageEntry> validMessages,
        Guid sessionId,
        int tokenBudget)
    {
        var directAddedTokens = 0;
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
        }
    }

    private async Task<int> AppendCompressedSummaryAsync(
        ChatHistory chatHistory,
        IReadOnlyList<HistoryMessageEntry> olderMessages,
        Guid sessionId,
        Guid? providerId,
        int remainingBudget,
        CancellationToken cancellationToken)
    {
        if (olderMessages.Count == 0 || !providerId.HasValue)
        {
            return remainingBudget;
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
            var summary = await _summaryService.GetOrGenerateSummaryAsync(
                olderContent.ToString(),
                targetChars,
                sessionId,
                providerId.Value,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(summary))
            {
                return remainingBudget;
            }

            var summaryMessage = $"[对话历史摘要]\n以下是本次对话早期内容的摘要，供参考：\n{summary}";
            var summaryTokens = ChatHistoryTextHelper.EstimateTokenCount(summaryMessage);

            chatHistory.AddUserMessage(summaryMessage);
            remainingBudget -= summaryTokens;

            _logger.LogDebug(
                "[AI.Chat] Compressed {Count} older messages into summary | SessionId={SessionId} SummaryTokens={Tokens}",
                olderMessages.Count,
                sessionId,
                summaryTokens);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[AI.Chat] Failed to generate summary for older messages, using truncation | SessionId={SessionId}",
                sessionId);
        }

        return remainingBudget;
    }

    private void AppendRecentMessages(
        ChatHistory chatHistory,
        IReadOnlyList<HistoryMessageEntry> recentMessages,
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
    }

    private sealed record HistoryMessageEntry(string SenderType, string Content);
}
