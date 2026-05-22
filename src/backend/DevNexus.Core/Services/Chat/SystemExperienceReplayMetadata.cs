using DevNexus.Shared.Constants;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 系统经验回放 metadata 写入工具。
/// </summary>
public static class SystemExperienceReplayMetadata
{
    /// <summary>
    /// 写入系统经验回放 metadata。
    /// </summary>
    public static void Apply(
        IDictionary<string, object> metadata,
        SystemExperienceReplayDecision decision)
    {
        if (decision.Match == null)
        {
            return;
        }

        metadata[ChatMessageMetadataKeys.SystemExperienceId] = decision.Match.Experience.Id;
        metadata[ChatMessageMetadataKeys.SystemExperienceSimilarity] = decision.Match.Similarity;
        metadata[ChatMessageMetadataKeys.SystemExperienceReplayReason] = decision.Reason;
        metadata[ChatMessageMetadataKeys.SystemExperienceContextTags] = decision.Match.Experience.ContextTags;
    }

    /// <summary>
    /// 写入缓存直接命中 metadata。
    /// </summary>
    public static void ApplyDirectHit(
        IDictionary<string, object> metadata,
        SystemExperienceReplayDecision decision)
    {
        if (decision.Match == null)
        {
            return;
        }

        metadata[ChatMessageMetadataKeys.CacheHit] = true;
        metadata[ChatMessageMetadataKeys.Similarity] = decision.Match.Similarity;
        Apply(metadata, decision);
    }

    /// <summary>
    /// 从 metadata 读取系统经验回放快照。
    /// </summary>
    public static SystemExperienceReplaySnapshot BuildSnapshot(
        IReadOnlyDictionary<string, object>? metadata)
    {
        if (!TryGetMetadataValue(metadata, ChatMessageMetadataKeys.SystemExperienceReplayReason, out var reason))
        {
            return SystemExperienceReplaySnapshot.Empty;
        }

        var contextTags = TryGetMetadataValue(metadata, ChatMessageMetadataKeys.SystemExperienceContextTags, out var tags)
            ? tags
            : string.Empty;

        return new SystemExperienceReplaySnapshot
            {
                HasMatch = true,
                AnsweredDirectly = string.Equals(reason, SystemExperienceReplayReasons.DirectAnswer, StringComparison.Ordinal),
                InjectedDynamicContext = string.Equals(reason, SystemExperienceReplayReasons.DynamicContext, StringComparison.Ordinal),
                Reason = reason,
                ExperienceId = TryGetMetadataGuid(metadata, ChatMessageMetadataKeys.SystemExperienceId),
                Similarity = TryGetMetadataFloat(metadata, ChatMessageMetadataKeys.SystemExperienceSimilarity),
                ContextTags = contextTags,
                ContextTagSnapshot = SystemExperienceContextTagSnapshot.Parse(contextTags)
            };
    }

    private static bool TryGetMetadataValue(
        IReadOnlyDictionary<string, object>? metadata,
        string key,
        out string value)
    {
        value = string.Empty;
        if (metadata == null || !metadata.TryGetValue(key, out var rawValue) || rawValue == null)
        {
            return false;
        }

        value = rawValue.ToString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static Guid? TryGetMetadataGuid(
        IReadOnlyDictionary<string, object>? metadata,
        string key)
    {
        return TryGetMetadataValue(metadata, key, out var value) && Guid.TryParse(value, out var id)
            ? id
            : null;
    }

    private static float? TryGetMetadataFloat(
        IReadOnlyDictionary<string, object>? metadata,
        string key)
    {
        return TryGetMetadataValue(metadata, key, out var value) && float.TryParse(value, out var number)
            ? number
            : null;
    }
}
