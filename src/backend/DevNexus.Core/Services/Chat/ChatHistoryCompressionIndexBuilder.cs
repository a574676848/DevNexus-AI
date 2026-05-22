using DevNexus.Shared.Constants;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 聊天历史压缩索引构建器。
/// </summary>
internal static class ChatHistoryCompressionIndexBuilder
{
    private const int MaxTopicCount = 3;
    private const int MaxTopicCharacters = 48;

    /// <summary>
    /// 根据被压缩的历史和摘要构建索引。
    /// </summary>
    public static ChatHistoryCompressionIndex Build(
        IReadOnlyList<ChatHistoryMessageEntry> compressedMessages,
        string? summary)
    {
        var normalizedSummary = summary?.Trim() ?? string.Empty;
        if (compressedMessages.Count == 0 || string.IsNullOrWhiteSpace(normalizedSummary))
        {
            return ChatHistoryCompressionIndex.Empty;
        }

        var topicHints = compressedMessages
            .Where(message => ChatConstants.IsUserSender(message.SenderType))
            .Select(message => NormalizeTopic(message.Content))
            .Where(topic => !string.IsNullOrWhiteSpace(topic))
            .Distinct(StringComparer.Ordinal)
            .Take(MaxTopicCount)
            .ToArray();

        if (topicHints.Length == 0)
        {
            topicHints = compressedMessages
                .Select(message => NormalizeTopic(message.Content))
                .Where(topic => !string.IsNullOrWhiteSpace(topic))
                .Distinct(StringComparer.Ordinal)
                .Take(MaxTopicCount)
                .ToArray();
        }

        return new ChatHistoryCompressionIndex
        {
            HasIndex = true,
            CoveredMessageCount = compressedMessages.Count,
            SummaryCharacterCount = normalizedSummary.Length,
            SummaryFingerprint = PromptFingerprint.ComputeHash(normalizedSummary),
            TopicHints = topicHints
        };
    }

    private static string NormalizeTopic(string content)
    {
        var normalized = string.Join(
            " ",
            content
                .Split(['\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim()));

        return normalized.Length <= MaxTopicCharacters
            ? normalized
            : normalized[..MaxTopicCharacters];
    }
}
