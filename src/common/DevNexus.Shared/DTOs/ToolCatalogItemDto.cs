namespace DevNexus.Shared.DTOs;

/// <summary>
/// 工具目录项。
/// </summary>
public sealed class ToolCatalogItemDto
{
    /// <summary>
    /// 插件名称。
    /// </summary>
    public required string PluginName { get; init; }

    /// <summary>
    /// 工具显示名称。
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// 工具分类。
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// 风险等级。
    /// </summary>
    public required string RiskLevel { get; init; }

    /// <summary>
    /// 是否属于核心工具。
    /// </summary>
    public bool IsCore { get; init; }

    /// <summary>
    /// 结果契约说明。
    /// </summary>
    public required string ResultContract { get; init; }
}
