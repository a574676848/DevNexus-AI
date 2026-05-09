using DevNexus.Shared.Enums;

namespace DevNexus.Client.Shared.Models;

/// <summary>
/// 会话运行态控制语义。
/// </summary>
public sealed class SessionRunControlState
{
    /// <summary>
    /// 当前统一运行态。
    /// </summary>
    public ChatSessionRunState RunState { get; set; } = ChatSessionRunState.Idle;

    /// <summary>
    /// 是否处于生成链路。
    /// </summary>
    public bool IsGenerationLike { get; set; }

    /// <summary>
    /// 是否允许取消。
    /// </summary>
    public bool CanCancel { get; set; }

    /// <summary>
    /// 是否为挂起交互阻塞态。
    /// </summary>
    public bool IsInteractionBlocking { get; set; }

    /// <summary>
    /// 是否为空闲态。
    /// </summary>
    public bool IsIdle { get; set; }
}
