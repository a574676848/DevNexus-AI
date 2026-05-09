using DevNexus.Shared.Enums;
using DevNexus.Client.Shared.Models;

namespace DevNexus.Client.Shared.Helpers;

/// <summary>
/// 统一管理会话运行态在客户端的展示文案与样式映射。
/// </summary>
public static class ChatSessionRunStateDisplay
{
    /// <summary>
    /// 获取统一运行态控制语义。
    /// </summary>
    public static SessionRunControlState GetControl(ChatSessionRunState state)
    {
        return new SessionRunControlState
        {
            RunState = state,
            IsGenerationLike = state.IsGenerationLike(),
            CanCancel = state is ChatSessionRunState.Generating
                or ChatSessionRunState.Running
                or ChatSessionRunState.WaitingForInput,
            IsInteractionBlocking = state is ChatSessionRunState.WaitingForPendingInput
                or ChatSessionRunState.WaitingForApproval,
            IsIdle = state == ChatSessionRunState.Idle
        };
    }

    /// <summary>
    /// 获取统一运行态展示状态。
    /// </summary>
    public static SessionRunPresentationState GetPresentation(ChatSessionRunState state)
    {
        var control = GetControl(state);
        return new SessionRunPresentationState
        {
            RunState = state,
            Description = GetDescriptionCore(state),
            CompactLabel = GetCompactLabelCore(state),
            ConnectionLabel = GetConnectionLabelCore(state),
            InputPlaceholder = GetInputPlaceholderCore(state),
            IndicatorClass = GetIndicatorClassCore(state),
            IsIdle = control.IsIdle,
            CanCancel = control.CanCancel,
            IsInteractionBlockingSend = control.IsInteractionBlocking,
            IsBusyWithoutCancel = state == ChatSessionRunState.Recovering,
            BusyLabel = state switch
            {
                ChatSessionRunState.Recovering => "恢复中...",
                ChatSessionRunState.WaitingForPendingInput or ChatSessionRunState.WaitingForApproval => GetDescriptionCore(state),
                _ => "处理中..."
            }
        };
    }

    /// <summary>
    /// 获取会话运行态的详细说明文案。
    /// </summary>
    public static string GetDescription(ChatSessionRunState state)
    {
        return GetDescriptionCore(state);
    }

    /// <summary>
    /// 获取侧边栏等紧凑场景使用的短标签。
    /// </summary>
    public static string GetCompactLabel(ChatSessionRunState state)
    {
        return GetCompactLabelCore(state);
    }

    /// <summary>
    /// 获取标题栏网络状态使用的标签。
    /// </summary>
    public static string? GetConnectionLabel(ChatSessionRunState state)
    {
        return GetConnectionLabelCore(state);
    }

    /// <summary>
    /// 获取输入框占位文案。
    /// </summary>
    public static string? GetInputPlaceholder(ChatSessionRunState state)
    {
        return GetInputPlaceholderCore(state);
    }

    /// <summary>
    /// 获取会话列表指示器样式类。
    /// </summary>
    public static string GetIndicatorClass(ChatSessionRunState state)
    {
        return GetIndicatorClassCore(state);
    }

    private static string GetDescriptionCore(ChatSessionRunState state)
    {
        return state switch
        {
            ChatSessionRunState.Generating => "消息已发送，正在生成回复...",
            ChatSessionRunState.Running => "终端仍在运行",
            ChatSessionRunState.WaitingForInput => "终端等待输入",
            ChatSessionRunState.WaitingForPendingInput => "等待补充信息",
            ChatSessionRunState.WaitingForApproval => "等待审批",
            ChatSessionRunState.Recovering => "连接恢复中，正在同步状态",
            ChatSessionRunState.Queued => "消息已排队，等待继续处理",
            _ => "等待输入"
        };
    }

    private static string GetCompactLabelCore(ChatSessionRunState state)
    {
        return state switch
        {
            ChatSessionRunState.Generating => "生成中",
            ChatSessionRunState.Running => "运行中",
            ChatSessionRunState.WaitingForInput => "终端待输",
            ChatSessionRunState.WaitingForPendingInput => "待补参",
            ChatSessionRunState.WaitingForApproval => "待审批",
            ChatSessionRunState.Recovering => "恢复中",
            ChatSessionRunState.Queued => "已排队",
            _ => "空闲"
        };
    }

    private static string? GetConnectionLabelCore(ChatSessionRunState state)
    {
        return state switch
        {
            ChatSessionRunState.Recovering => "恢复中",
            ChatSessionRunState.WaitingForPendingInput => "待补参",
            ChatSessionRunState.WaitingForApproval => "待审批",
            ChatSessionRunState.WaitingForInput => "等待输入",
            ChatSessionRunState.Running => "运行中",
            ChatSessionRunState.Queued => "已排队",
            _ => null
        };
    }

    private static string? GetInputPlaceholderCore(ChatSessionRunState state)
    {
        return state switch
        {
            ChatSessionRunState.Recovering => "连接恢复中，正在同步当前会话",
            ChatSessionRunState.Queued => "消息已排队，等待继续处理",
            ChatSessionRunState.WaitingForPendingInput => "请先完成上方待补充信息",
            ChatSessionRunState.WaitingForApproval => "当前等待审批，审批通过后可继续",
            _ => null
        };
    }

    private static string GetIndicatorClassCore(ChatSessionRunState state)
    {
        return state switch
        {
            ChatSessionRunState.Generating => "session-run-indicator--generating",
            ChatSessionRunState.Running => "session-run-indicator--running",
            ChatSessionRunState.WaitingForInput => "session-run-indicator--input",
            ChatSessionRunState.WaitingForPendingInput => "session-run-indicator--pending-input",
            ChatSessionRunState.WaitingForApproval => "session-run-indicator--approval",
            ChatSessionRunState.Recovering => "session-run-indicator--recovering",
            ChatSessionRunState.Queued => "session-run-indicator--queued",
            _ => string.Empty
        };
    }
}
