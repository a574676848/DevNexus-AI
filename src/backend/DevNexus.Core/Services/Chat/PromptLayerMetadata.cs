namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Prompt 分层构建的审计元数据。
/// </summary>
public sealed class PromptLayerMetadata
{
    /// <summary>
    /// 稳定前缀文本。
    /// </summary>
    public string StablePrefix { get; init; } = string.Empty;

    /// <summary>
    /// 稳定前缀指纹。
    /// </summary>
    public string StablePrefixHash { get; init; } = string.Empty;

    /// <summary>
    /// 稳定前缀片段清单。
    /// </summary>
    public IReadOnlyList<PromptFragmentManifestItem> StablePrefixManifest { get; init; } =
        Array.Empty<PromptFragmentManifestItem>();

    /// <summary>
    /// 动态上下文片段清单。
    /// </summary>
    public IReadOnlyList<PromptFragmentManifestItem> DynamicContextManifest { get; init; } =
        Array.Empty<PromptFragmentManifestItem>();

    /// <summary>
    /// Prompt 缓存键。
    /// </summary>
    public string PromptCacheKey { get; set; } = string.Empty;

    /// <summary>
    /// 动态上下文文本。
    /// </summary>
    public string DynamicContext { get; init; } = string.Empty;

    /// <summary>
    /// 动态上下文 Token 数。
    /// </summary>
    public int DynamicContextTokens { get; init; }

    /// <summary>
    /// 历史消息 Token 数。
    /// </summary>
    public int HistoryTokens { get; set; }

    /// <summary>
    /// 历史上下文治理快照。
    /// </summary>
    public ChatHistoryGovernanceSnapshot HistoryGovernance { get; set; } =
        ChatHistoryGovernanceSnapshot.Empty;

    /// <summary>
    /// 系统经验回放快照。
    /// </summary>
    public SystemExperienceReplaySnapshot SystemExperienceReplay { get; set; } =
        SystemExperienceReplaySnapshot.Empty;

    /// <summary>
    /// Prompt 缓存标记候选数量。
    /// </summary>
    public int CacheMarkerCandidateCount { get; set; }

    /// <summary>
    /// 是否具备双标记缓存预热条件。
    /// </summary>
    public bool CacheDoubleMarkerReady { get; set; }

    /// <summary>
    /// 双标记缓存预热状态原因。
    /// </summary>
    public string? CacheMarkerReadinessReason { get; set; }

    /// <summary>
    /// 工具 Schema 与排序指纹。
    /// </summary>
    public string? ToolSchemaHash { get; set; }

    /// <summary>
    /// Skill 规范摘要指纹。
    /// </summary>
    public string? SkillInstructionHash { get; init; }
}
