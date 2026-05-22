namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Prompt 缓存漂移原因。
/// </summary>
public enum PromptCacheDriftReason
{
    /// <summary>
    /// 未发生漂移。
    /// </summary>
    None = 0,

    /// <summary>
    /// 稳定前缀发生变化。
    /// </summary>
    StablePrefixChanged = 1,

    /// <summary>
    /// 工具 Schema 发生变化。
    /// </summary>
    ToolSchemaChanged = 2,

    /// <summary>
    /// 稳定前缀片段清单发生变化。
    /// </summary>
    StablePrefixManifestChanged = 3,

    /// <summary>
    /// 缓存键缺失，无法判断。
    /// </summary>
    MissingCacheKey = 4,

    /// <summary>
    /// 缓存键变化但未能归因。
    /// </summary>
    Unknown = 5
}

/// <summary>
/// Prompt 片段漂移类型。
/// </summary>
public enum PromptFragmentDriftKind
{
    /// <summary>
    /// 片段发生修改。
    /// </summary>
    Modified = 0,

    /// <summary>
    /// 片段被新增。
    /// </summary>
    Added = 1,

    /// <summary>
    /// 片段被移除。
    /// </summary>
    Removed = 2
}

/// <summary>
/// Prompt 片段漂移项。
/// </summary>
public sealed record PromptFragmentDriftItem
{
    /// <summary>
    /// 片段槽位。
    /// </summary>
    public string Slot { get; init; } = string.Empty;

    /// <summary>
    /// 片段顺序。
    /// </summary>
    public int Sequence { get; init; }

    /// <summary>
    /// 变化前片段来源标识。
    /// </summary>
    public string? PreviousSource { get; init; }

    /// <summary>
    /// 变化后片段来源标识。
    /// </summary>
    public string? CurrentSource { get; init; }

    /// <summary>
    /// 漂移类型。
    /// </summary>
    public PromptFragmentDriftKind Kind { get; init; }

    /// <summary>
    /// 变化前字符数。
    /// </summary>
    public int? PreviousCharacterCount { get; init; }

    /// <summary>
    /// 变化后字符数。
    /// </summary>
    public int? CurrentCharacterCount { get; init; }

    /// <summary>
    /// 变化前内容指纹。
    /// </summary>
    public string? PreviousTextHash { get; init; }

    /// <summary>
    /// 变化后内容指纹。
    /// </summary>
    public string? CurrentTextHash { get; init; }
}

/// <summary>
/// Prompt 缓存漂移分析结果。
/// </summary>
public sealed record PromptCacheDriftAnalysis
{
    /// <summary>
    /// 漂移原因。
    /// </summary>
    public PromptCacheDriftReason Reason { get; init; }

    /// <summary>
    /// 发生变化的稳定前缀片段。
    /// </summary>
    public IReadOnlyList<PromptFragmentDriftItem> ChangedFragments { get; init; } = Array.Empty<PromptFragmentDriftItem>();
}

/// <summary>
/// Prompt 缓存快照。
/// </summary>
public sealed record PromptCacheSnapshot(
    string? PromptCacheKey,
    string? StablePrefixHash,
    string? ToolSchemaHash,
    IReadOnlyList<PromptFragmentManifestItem>? StablePrefixManifest = null);

/// <summary>
/// Prompt 缓存漂移分析器。
/// </summary>
public static class PromptCacheDriftAnalyzer
{
    /// <summary>
    /// 分析两个相邻请求的缓存漂移原因。
    /// </summary>
    public static PromptCacheDriftReason Analyze(PromptCacheSnapshot previous, PromptCacheSnapshot current)
    {
        return AnalyzeDetailed(previous, current).Reason;
    }

    /// <summary>
    /// 分析两个相邻请求的缓存漂移原因和片段级变化。
    /// </summary>
    public static PromptCacheDriftAnalysis AnalyzeDetailed(PromptCacheSnapshot previous, PromptCacheSnapshot current)
    {
        if (IsBlank(previous.PromptCacheKey) || IsBlank(current.PromptCacheKey))
        {
            return CreateAnalysis(PromptCacheDriftReason.MissingCacheKey);
        }

        if (EqualsNormalized(previous.PromptCacheKey, current.PromptCacheKey))
        {
            return CreateAnalysis(PromptCacheDriftReason.None);
        }

        if (!EqualsNormalized(previous.StablePrefixHash, current.StablePrefixHash))
        {
            var changedFragments = BuildChangedFragments(
                previous.StablePrefixManifest,
                current.StablePrefixManifest);
            if (changedFragments.Count > 0)
            {
                return CreateAnalysis(PromptCacheDriftReason.StablePrefixManifestChanged, changedFragments);
            }

            return CreateAnalysis(PromptCacheDriftReason.StablePrefixChanged);
        }

        if (!EqualsNormalized(previous.ToolSchemaHash, current.ToolSchemaHash))
        {
            return CreateAnalysis(PromptCacheDriftReason.ToolSchemaChanged);
        }

        return CreateAnalysis(PromptCacheDriftReason.Unknown);
    }

    private static bool EqualsNormalized(string? left, string? right)
    {
        return string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);
    }

    private static string Normalize(string? value)
    {
        return value?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static bool IsBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value);
    }

    private static IReadOnlyList<PromptFragmentDriftItem> BuildChangedFragments(
        IReadOnlyList<PromptFragmentManifestItem>? previous,
        IReadOnlyList<PromptFragmentManifestItem>? current)
    {
        if (previous == null || current == null || previous.Count == 0 || current.Count == 0)
        {
            return Array.Empty<PromptFragmentDriftItem>();
        }

        var previousByKey = previous.ToDictionary(CreateManifestKey, StringComparer.Ordinal);
        var currentByKey = current.ToDictionary(CreateManifestKey, StringComparer.Ordinal);
        var changed = new List<PromptFragmentDriftItem>();

        foreach (var previousItem in previous)
        {
            var key = CreateManifestKey(previousItem);
            if (!currentByKey.TryGetValue(key, out var currentItem))
            {
                changed.Add(CreateRemovedFragment(previousItem));
                continue;
            }

            if (!IsSameManifestItem(previousItem, currentItem))
            {
                changed.Add(CreateModifiedFragment(previousItem, currentItem));
            }
        }

        foreach (var currentItem in current)
        {
            if (!previousByKey.ContainsKey(CreateManifestKey(currentItem)))
            {
                changed.Add(CreateAddedFragment(currentItem));
            }
        }

        return changed
            .OrderBy(item => item.Slot, StringComparer.Ordinal)
            .ThenBy(item => item.Sequence)
            .ToList();
    }

    private static bool IsSameManifestItem(
        PromptFragmentManifestItem previous,
        PromptFragmentManifestItem current)
    {
        return string.Equals(previous.Slot, current.Slot, StringComparison.Ordinal)
               && previous.Sequence == current.Sequence
               && previous.CharacterCount == current.CharacterCount
               && string.Equals(previous.TextHash, current.TextHash, StringComparison.Ordinal);
    }

    private static string CreateManifestKey(PromptFragmentManifestItem item)
    {
        return $"{item.Slot}:{item.Sequence}";
    }

    private static PromptCacheDriftAnalysis CreateAnalysis(
        PromptCacheDriftReason reason,
        IReadOnlyList<PromptFragmentDriftItem>? changedFragments = null)
    {
        return new PromptCacheDriftAnalysis
        {
            Reason = reason,
            ChangedFragments = changedFragments ?? Array.Empty<PromptFragmentDriftItem>()
        };
    }

    private static PromptFragmentDriftItem CreateModifiedFragment(
        PromptFragmentManifestItem previous,
        PromptFragmentManifestItem current)
    {
        return new PromptFragmentDriftItem
        {
            Slot = current.Slot,
            Sequence = current.Sequence,
            PreviousSource = previous.Source,
            CurrentSource = current.Source,
            Kind = PromptFragmentDriftKind.Modified,
            PreviousCharacterCount = previous.CharacterCount,
            CurrentCharacterCount = current.CharacterCount,
            PreviousTextHash = previous.TextHash,
            CurrentTextHash = current.TextHash
        };
    }

    private static PromptFragmentDriftItem CreateAddedFragment(PromptFragmentManifestItem current)
    {
        return new PromptFragmentDriftItem
        {
            Slot = current.Slot,
            Sequence = current.Sequence,
            CurrentSource = current.Source,
            Kind = PromptFragmentDriftKind.Added,
            CurrentCharacterCount = current.CharacterCount,
            CurrentTextHash = current.TextHash
        };
    }

    private static PromptFragmentDriftItem CreateRemovedFragment(PromptFragmentManifestItem previous)
    {
        return new PromptFragmentDriftItem
        {
            Slot = previous.Slot,
            Sequence = previous.Sequence,
            PreviousSource = previous.Source,
            Kind = PromptFragmentDriftKind.Removed,
            PreviousCharacterCount = previous.CharacterCount,
            PreviousTextHash = previous.TextHash
        };
    }
}
