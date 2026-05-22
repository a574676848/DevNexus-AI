namespace DevNexus.Shared.Enums;

/// <summary>
/// 工具执行后的建议动作。
/// </summary>
public enum ToolSuggestedAction
{
    /// <summary>
    /// 无建议动作。
    /// </summary>
    None = 0,

    /// <summary>
    /// 继续重试。
    /// </summary>
    Retry = 1,

    /// <summary>
    /// 刷新凭证。
    /// </summary>
    RefreshCredential = 2,

    /// <summary>
    /// 请求用户输入。
    /// </summary>
    PromptUserInput = 3,

    /// <summary>
    /// 请求用户审批。
    /// </summary>
    RequestApproval = 4,

    /// <summary>
    /// 走降级或备用路线。
    /// </summary>
    Fallback = 5,

    /// <summary>
    /// 终止执行。
    /// </summary>
    Abort = 6,

    /// <summary>
    /// 等待运行中的任务完成。
    /// </summary>
    WaitForCompletion = 7,

    /// <summary>
    /// 停止当前终端命令。
    /// </summary>
    StopCommand = 8
}

/// <summary>
/// 工具建议动作字符串协议扩展。
/// </summary>
public static class ToolSuggestedActionExtensions
{
    private static readonly IReadOnlyList<ToolSuggestedAction> RecoveryPriority = new[]
    {
        ToolSuggestedAction.RequestApproval,
        ToolSuggestedAction.PromptUserInput,
        ToolSuggestedAction.RefreshCredential,
        ToolSuggestedAction.StopCommand,
        ToolSuggestedAction.WaitForCompletion,
        ToolSuggestedAction.Retry,
        ToolSuggestedAction.Fallback,
        ToolSuggestedAction.Abort
    };

    /// <summary>
    /// 按恢复优先级返回建议动作。
    /// </summary>
    public static IReadOnlyList<ToolSuggestedAction> GetRecoveryPriority()
    {
        return RecoveryPriority;
    }

    /// <summary>
    /// 转换为批次诊断展示短文本。
    /// </summary>
    public static string ToDiagnosticText(this ToolSuggestedAction action)
    {
        return action switch
        {
            ToolSuggestedAction.RequestApproval => "等待审批",
            ToolSuggestedAction.PromptUserInput => "补充输入",
            ToolSuggestedAction.RefreshCredential => "刷新凭证",
            ToolSuggestedAction.StopCommand => "停止命令",
            ToolSuggestedAction.WaitForCompletion => "等待完成",
            ToolSuggestedAction.Retry => "建议重试",
            ToolSuggestedAction.Fallback => "建议降级",
            ToolSuggestedAction.Abort => "建议终止",
            _ => "无需动作"
        };
    }

    /// <summary>
    /// 转换为前后端传输使用的字符串值。
    /// </summary>
    public static string ToWireValue(this ToolSuggestedAction action)
    {
        return action switch
        {
            ToolSuggestedAction.None => nameof(ToolSuggestedAction.None),
            ToolSuggestedAction.Retry => nameof(ToolSuggestedAction.Retry),
            ToolSuggestedAction.RefreshCredential => nameof(ToolSuggestedAction.RefreshCredential),
            ToolSuggestedAction.PromptUserInput => nameof(ToolSuggestedAction.PromptUserInput),
            ToolSuggestedAction.RequestApproval => nameof(ToolSuggestedAction.RequestApproval),
            ToolSuggestedAction.Fallback => nameof(ToolSuggestedAction.Fallback),
            ToolSuggestedAction.Abort => nameof(ToolSuggestedAction.Abort),
            ToolSuggestedAction.WaitForCompletion => nameof(ToolSuggestedAction.WaitForCompletion),
            ToolSuggestedAction.StopCommand => nameof(ToolSuggestedAction.StopCommand),
            _ => nameof(ToolSuggestedAction.None)
        };
    }

    /// <summary>
    /// 从字符串协议值解析为枚举。
    /// </summary>
    public static ToolSuggestedAction Parse(string? value)
    {
        var normalized = value?.Trim();

        return normalized switch
        {
            nameof(ToolSuggestedAction.Retry) => ToolSuggestedAction.Retry,
            nameof(ToolSuggestedAction.RefreshCredential) => ToolSuggestedAction.RefreshCredential,
            nameof(ToolSuggestedAction.PromptUserInput) => ToolSuggestedAction.PromptUserInput,
            nameof(ToolSuggestedAction.RequestApproval) => ToolSuggestedAction.RequestApproval,
            nameof(ToolSuggestedAction.Fallback) => ToolSuggestedAction.Fallback,
            nameof(ToolSuggestedAction.Abort) => ToolSuggestedAction.Abort,
            nameof(ToolSuggestedAction.WaitForCompletion) => ToolSuggestedAction.WaitForCompletion,
            nameof(ToolSuggestedAction.StopCommand) => ToolSuggestedAction.StopCommand,
            _ => ToolSuggestedAction.None
        };
    }
}
