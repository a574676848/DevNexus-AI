using DevNexus.Shared.Enums;

namespace DevNexus.Client.Shared.Models;

/// <summary>
/// AI 消息附近展示的工具活动状态。
/// </summary>
public sealed class ToolActivityPresentationState
{
    /// <summary>
    /// 会话标识。
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 工具调用标识。
    /// </summary>
    public Guid ToolCallId { get; set; }

    /// <summary>
    /// 工具完整名称。
    /// </summary>
    public string ToolName { get; set; } = "工具";

    /// <summary>
    /// 工具调用状态。
    /// </summary>
    public ToolInvocationStatus Status { get; set; } = ToolInvocationStatus.Unknown;

    /// <summary>
    /// 短标签。
    /// </summary>
    public string Label { get; set; } = "处理中";

    /// <summary>
    /// 悬停说明。
    /// </summary>
    public string Title { get; set; } = "工具正在处理";

    /// <summary>
    /// 色调样式类。
    /// </summary>
    public string ToneClass { get; set; } = "ai-activity-chip--running";

    /// <summary>
    /// 是否仍应作为活跃状态展示。
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 最近更新时间。
    /// </summary>
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}
