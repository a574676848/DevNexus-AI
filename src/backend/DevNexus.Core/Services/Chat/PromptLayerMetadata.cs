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
    /// 工具 Schema 与排序指纹。
    /// </summary>
    public string? ToolSchemaHash { get; set; }

    /// <summary>
    /// Skill 规范摘要指纹。
    /// </summary>
    public string? SkillInstructionHash { get; init; }
}
