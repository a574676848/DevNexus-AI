namespace DevNexus.Shared.Enums;

/// <summary>
/// 会话统一运行状态。
/// </summary>
public enum ChatSessionRunState
{
    Idle = 0,
    Queued = 1,
    Generating = 2,
    WaitingForInput = 3,
    Running = 4,
    Recovering = 5,
    WaitingForPendingInput = 6,
    WaitingForApproval = 7
}

/// <summary>
/// 会话统一运行状态扩展。
/// </summary>
public static class ChatSessionRunStateExtensions
{
    /// <summary>
    /// 将字符串运行态解析为统一运行态。
    /// </summary>
    public static ChatSessionRunState Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ChatSessionRunState.Idle;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "queued" => ChatSessionRunState.Queued,
            "generating" => ChatSessionRunState.Generating,
            "waitingforinput" => ChatSessionRunState.WaitingForInput,
            "running" => ChatSessionRunState.Running,
            "recovering" => ChatSessionRunState.Recovering,
            "waitingforpendinginput" => ChatSessionRunState.WaitingForPendingInput,
            "waitingforapproval" => ChatSessionRunState.WaitingForApproval,
            _ => ChatSessionRunState.Idle
        };
    }

    /// <summary>
    /// 转换为跨端稳定字符串值。
    /// </summary>
    public static string ToWireValue(this ChatSessionRunState state)
    {
        return state switch
        {
            ChatSessionRunState.Queued => nameof(ChatSessionRunState.Queued),
            ChatSessionRunState.Generating => nameof(ChatSessionRunState.Generating),
            ChatSessionRunState.WaitingForInput => nameof(ChatSessionRunState.WaitingForInput),
            ChatSessionRunState.Running => nameof(ChatSessionRunState.Running),
            ChatSessionRunState.Recovering => nameof(ChatSessionRunState.Recovering),
            ChatSessionRunState.WaitingForPendingInput => nameof(ChatSessionRunState.WaitingForPendingInput),
            ChatSessionRunState.WaitingForApproval => nameof(ChatSessionRunState.WaitingForApproval),
            _ => nameof(ChatSessionRunState.Idle)
        };
    }

    /// <summary>
    /// 是否属于流式生成链路中的忙碌态。
    /// </summary>
    public static bool IsGenerationLike(this ChatSessionRunState state)
    {
        return state is ChatSessionRunState.Generating or ChatSessionRunState.Recovering;
    }

    /// <summary>
    /// 当前运行态是否允许取消。
    /// </summary>
    public static bool CanCancel(this ChatSessionRunState state)
    {
        return state is ChatSessionRunState.Generating
            or ChatSessionRunState.Running
            or ChatSessionRunState.WaitingForInput;
    }
}
