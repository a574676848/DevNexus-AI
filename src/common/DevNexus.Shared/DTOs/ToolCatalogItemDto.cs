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
    /// 工具暴露模式。
    /// </summary>
    public required string ExposureMode { get; init; }

    /// <summary>
    /// 结果契约说明。
    /// </summary>
    public required string ResultContract { get; init; }

    /// <summary>
    /// 是否要求工具输出使用统一结果标签。
    /// </summary>
    public bool RequiresTaggedOutput { get; init; }

    /// <summary>
    /// 工具是否支持同一轮内并行执行。
    /// </summary>
    public bool SupportsParallelExecution { get; init; }

    /// <summary>
    /// 可解析到该插件的工具别名。
    /// </summary>
    public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();
}
