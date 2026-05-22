using Microsoft.SemanticKernel.ChatCompletion;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Prompt 缓存标记计划。
/// </summary>
public sealed record PromptCacheMarkerPlan
{
    /// <summary>
    /// 缓存标记就绪原因：已具备双标记。
    /// </summary>
    public const string ReadyReason = "double-marker-ready";

    /// <summary>
    /// 缓存标记就绪原因：真实对话消息不足。
    /// </summary>
    public const string InsufficientConversationMessagesReason = "insufficient-conversation-messages";

    /// <summary>
    /// 计划标记的聊天消息索引。
    /// </summary>
    public IReadOnlyList<int> MarkerIndexes { get; init; } = Array.Empty<int>();

    /// <summary>
    /// 是否具备双标记预热条件。
    /// </summary>
    public bool IsDoubleMarkerReady => MarkerIndexes.Count >= PromptCacheMarkerPlanner.TargetMarkerCount;

    /// <summary>
    /// 双标记就绪原因。
    /// </summary>
    public string ReadinessReason { get; init; } = InsufficientConversationMessagesReason;
}

/// <summary>
/// Prompt 缓存标记规划器。
/// </summary>
public static class PromptCacheMarkerPlanner
{
    /// <summary>
    /// 目标缓存标记数量。
    /// </summary>
    public const int TargetMarkerCount = 2;

    /// <summary>
    /// 从聊天历史中选择最近的真实对话消息作为缓存标记候选。
    /// </summary>
    public static PromptCacheMarkerPlan Plan(ChatHistory chatHistory)
    {
        ArgumentNullException.ThrowIfNull(chatHistory);

        var markerIndexes = chatHistory
            .Select((message, index) => new { Message = message, Index = index })
            .Where(item => IsConversationRole(item.Message.Role))
            .Where(item => !PromptDynamicContextMessageBuilder.IsSystemInjected(item.Message.Content))
            .Where(item => !string.IsNullOrWhiteSpace(item.Message.Content))
            .Select(item => item.Index)
            .TakeLast(TargetMarkerCount)
            .ToList();

        return new PromptCacheMarkerPlan
        {
            MarkerIndexes = markerIndexes,
            ReadinessReason = markerIndexes.Count >= TargetMarkerCount
                ? PromptCacheMarkerPlan.ReadyReason
                : PromptCacheMarkerPlan.InsufficientConversationMessagesReason
        };
    }

    private static bool IsConversationRole(AuthorRole role)
    {
        return role == AuthorRole.User || role == AuthorRole.Assistant;
    }
}
