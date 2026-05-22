using DevNexus.Shared.Constants;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 聊天最近历史切片策略。
/// </summary>
internal static class ChatHistoryRecentSlicePolicy
{
    /// <summary>
    /// 选择最近历史，并确保压缩摘要之后的片段具备用户锚点。
    /// </summary>
    public static IReadOnlyList<ChatHistoryMessageEntry> Select(
        IReadOnlyList<ChatHistoryMessageEntry> messages,
        int maxCount)
    {
        if (messages.Count == 0 || maxCount <= 0)
        {
            return Array.Empty<ChatHistoryMessageEntry>();
        }

        var slice = messages.TakeLast(maxCount).ToList();
        var firstUserIndex = slice.FindIndex(message => ChatConstants.IsUserSender(message.SenderType));
        if (firstUserIndex < 0)
        {
            return Array.Empty<ChatHistoryMessageEntry>();
        }

        return firstUserIndex == 0
            ? slice
            : slice.Skip(firstUserIndex).ToList();
    }
}
