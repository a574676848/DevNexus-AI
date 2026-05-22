namespace DevNexus.Shared.DTOs;

/// <summary>
/// Prompt 优化审计元数据。
/// </summary>
public sealed class PromptOptimizationMetadataDto
{
    /// <summary>
    /// Prompt 缓存键。
    /// </summary>
    public string? PromptCacheKey { get; init; }

    /// <summary>
    /// 稳定 Prompt 前缀指纹。
    /// </summary>
    public string? StablePrefixHash { get; init; }

    /// <summary>
    /// 工具 Schema 与排序指纹。
    /// </summary>
    public string? ToolSchemaHash { get; init; }

    /// <summary>
    /// 动态上下文 Token 数。
    /// </summary>
    public int? DynamicContextTokens { get; init; }

    /// <summary>
    /// 历史消息 Token 数。
    /// </summary>
    public int? HistoryTokens { get; init; }

    /// <summary>
    /// Prompt 缓存标记候选数量。
    /// </summary>
    public int? CacheMarkerCandidateCount { get; init; }

    /// <summary>
    /// 是否具备双标记缓存预热条件。
    /// </summary>
    public bool? CacheDoubleMarkerReady { get; init; }

    /// <summary>
    /// 双标记缓存预热状态原因。
    /// </summary>
    public string? CacheMarkerReadinessReason { get; init; }

    /// <summary>
    /// 稳定 Prompt 前缀片段清单。
    /// </summary>
    public IReadOnlyList<PromptFragmentManifestItemDto> StablePrefixManifest { get; init; } =
        Array.Empty<PromptFragmentManifestItemDto>();

    /// <summary>
    /// 动态上下文片段清单。
    /// </summary>
    public IReadOnlyList<PromptFragmentManifestItemDto> DynamicContextManifest { get; init; } =
        Array.Empty<PromptFragmentManifestItemDto>();
}

/// <summary>
/// Prompt 片段清单项。
/// </summary>
public sealed class PromptFragmentManifestItemDto
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
    /// 片段来源标识。
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// 片段字符数。
    /// </summary>
    public int CharacterCount { get; init; }

    /// <summary>
    /// 片段内容指纹。
    /// </summary>
    public string TextHash { get; init; } = string.Empty;
}
