using DevNexus.Shared.Enums;

namespace DevNexus.Client.Shared.Models;

/// <summary>
/// 会话运行态展示状态。
/// </summary>
public sealed class SessionRunPresentationState
{
    /// <summary>
    /// 当前运行态。
    /// </summary>
    public ChatSessionRunState RunState { get; set; } = ChatSessionRunState.Idle;

    /// <summary>
    /// 详细描述文案。
    /// </summary>
    public string Description { get; set; } = "等待输入";

    /// <summary>
    /// 紧凑标签。
    /// </summary>
    public string CompactLabel { get; set; } = "空闲";

    /// <summary>
    /// 标题栏连接标签。
    /// </summary>
    public string? ConnectionLabel { get; set; }

    /// <summary>
    /// 输入框占位文案。
    /// </summary>
    public string? InputPlaceholder { get; set; }

    /// <summary>
    /// 会话列表指示器样式类。
    /// </summary>
    public string IndicatorClass { get; set; } = string.Empty;

    /// <summary>
    /// 是否为空闲态。
    /// </summary>
    public bool IsIdle { get; set; }

    /// <summary>
    /// 是否可取消。
    /// </summary>
    public bool CanCancel { get; set; }

    /// <summary>
    /// 是否阻塞发送。
    /// </summary>
    public bool IsInteractionBlockingSend { get; set; }

    /// <summary>
    /// 是否处于无取消按钮的忙碌态。
    /// </summary>
    public bool IsBusyWithoutCancel { get; set; }

    /// <summary>
    /// 忙碌标签。
    /// </summary>
    public string BusyLabel { get; set; } = "处理中...";
}
