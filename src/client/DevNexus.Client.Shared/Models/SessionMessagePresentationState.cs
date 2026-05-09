using DevNexus.Shared.Enums;

namespace DevNexus.Client.Shared.Models;

/// <summary>
/// 会话消息展示状态。
/// </summary>
public sealed class SessionMessagePresentationState
{
    /// <summary>
    /// 会话标识。
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 当前统一运行态。
    /// </summary>
    public ChatSessionRunState RunState { get; set; } = ChatSessionRunState.Idle;

    /// <summary>
    /// 是否处于消息流式展示阶段。
    /// </summary>
    public bool IsStreaming { get; set; }

    /// <summary>
    /// 详细状态文案。
    /// </summary>
    public string StatusText { get; set; } = "等待输入";

    /// <summary>
    /// 紧凑状态文案。
    /// </summary>
    public string CompactStatusText { get; set; } = "空闲";
}
