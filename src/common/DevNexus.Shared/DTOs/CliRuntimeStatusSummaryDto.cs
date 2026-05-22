namespace DevNexus.Shared.DTOs;

/// <summary>
/// CLI 运行时状态摘要 DTO。
/// </summary>
public sealed class CliRuntimeStatusSummaryDto
{
    /// <summary>
    /// 视觉语义。
    /// </summary>
    public string Tone { get; set; } = "neutral";

    /// <summary>
    /// 状态标签。
    /// </summary>
    public string Label { get; set; } = "未知";

    /// <summary>
    /// 低噪说明文案。
    /// </summary>
    public string Description { get; set; } = "查看终端详情";

    /// <summary>
    /// 建议下一步动作。
    /// </summary>
    public string NextAction { get; set; } = "ViewDetails";

    /// <summary>
    /// 是否需要用户输入。
    /// </summary>
    public bool RequiresInput { get; set; }

    /// <summary>
    /// 是否处于终态。
    /// </summary>
    public bool IsTerminal { get; set; }

    /// <summary>
    /// 终止原因显示文本。
    /// </summary>
    public string TerminationReasonText { get; set; } = string.Empty;
}
