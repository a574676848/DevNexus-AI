namespace DevNexus.Shared.DTOs;

/// <summary>
/// Prompt 优化审计元数据。
/// </summary>
public sealed class PromptOptimizationMetadataDto
{
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
}
