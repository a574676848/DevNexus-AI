namespace DevNexus.Client.Shared.Models;

/// <summary>
/// 统一终端展示状态。
/// </summary>
public sealed class TerminalPresentationState
{
    /// <summary>
    /// 会话标识。
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 主终端记录标识。
    /// </summary>
    public Guid? RecordId { get; set; }

    /// <summary>
    /// 展示标题。
    /// </summary>
    public string Headline { get; set; } = "终端";

    /// <summary>
    /// 状态标签。
    /// </summary>
    public string StatusLabel { get; set; } = "未知";

    /// <summary>
    /// 描述文案。
    /// </summary>
    public string Description { get; set; } = "查看终端详情";

    /// <summary>
    /// 摘要元信息。
    /// </summary>
    public string MetaLine { get; set; } = "查看终端详情";

    /// <summary>
    /// 色调样式类。
    /// </summary>
    public string ToneClass { get; set; } = "terminal-summary-card--neutral";

    /// <summary>
    /// 是否处于等待输入状态。
    /// </summary>
    public bool WaitingForInput { get; set; }

    /// <summary>
    /// 是否存在活跃终端。
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// 统一运行态标签。
    /// </summary>
    public string RunStateLabel { get; set; } = string.Empty;

    /// <summary>
    /// 工作目录。
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// 命令文本。
    /// </summary>
    public string? Command { get; set; }

    /// <summary>
    /// 观察摘要。
    /// </summary>
    public string? WatchSummary { get; set; }

    /// <summary>
    /// 会话范围文案。
    /// </summary>
    public string ScopeLabel { get; set; } = string.Empty;

    /// <summary>
    /// 交互模式文案。
    /// </summary>
    public string ModeLabel { get; set; } = "聊天执行";
}
