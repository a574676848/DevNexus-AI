namespace DevNexus.Shared.Enums;

/// <summary>
/// 工具执行失败原因。
/// </summary>
public enum ToolFailureReason
{
    /// <summary>
    /// 无失败。
    /// </summary>
    None = 0,

    /// <summary>
    /// 认证已过期。
    /// </summary>
    AuthExpired = 1,

    /// <summary>
    /// 认证永久失败。
    /// </summary>
    AuthPermanent = 2,

    /// <summary>
    /// 缺少用户输入。
    /// </summary>
    MissingUserInput = 3,

    /// <summary>
    /// 需要审批。
    /// </summary>
    ApprovalRequired = 4,

    /// <summary>
    /// 权限不足。
    /// </summary>
    PermissionDenied = 5,

    /// <summary>
    /// 路径不存在。
    /// </summary>
    PathNotFound = 6,

    /// <summary>
    /// 触发限流。
    /// </summary>
    RateLimited = 7,

    /// <summary>
    /// 账单或额度受限。
    /// </summary>
    BillingLimited = 8,

    /// <summary>
    /// 上下文过长。
    /// </summary>
    ContextOverflow = 9,

    /// <summary>
    /// 工具返回格式异常。
    /// </summary>
    ToolFormatError = 10,

    /// <summary>
    /// 短暂网络失败。
    /// </summary>
    TransientNetworkFailure = 11,

    /// <summary>
    /// 致命执行错误。
    /// </summary>
    FatalExecutionError = 12,

    /// <summary>
    /// 未知原因。
    /// </summary>
    Unknown = 13
}

/// <summary>
/// 工具失败原因字符串协议扩展。
/// </summary>
public static class ToolFailureReasonExtensions
{
    /// <summary>
    /// 转换为前后端传输使用的字符串值。
    /// </summary>
    public static string ToWireValue(this ToolFailureReason reason)
    {
        return reason switch
        {
            ToolFailureReason.None => nameof(ToolFailureReason.None),
            ToolFailureReason.AuthExpired => nameof(ToolFailureReason.AuthExpired),
            ToolFailureReason.AuthPermanent => nameof(ToolFailureReason.AuthPermanent),
            ToolFailureReason.MissingUserInput => nameof(ToolFailureReason.MissingUserInput),
            ToolFailureReason.ApprovalRequired => nameof(ToolFailureReason.ApprovalRequired),
            ToolFailureReason.PermissionDenied => nameof(ToolFailureReason.PermissionDenied),
            ToolFailureReason.PathNotFound => nameof(ToolFailureReason.PathNotFound),
            ToolFailureReason.RateLimited => nameof(ToolFailureReason.RateLimited),
            ToolFailureReason.BillingLimited => nameof(ToolFailureReason.BillingLimited),
            ToolFailureReason.ContextOverflow => nameof(ToolFailureReason.ContextOverflow),
            ToolFailureReason.ToolFormatError => nameof(ToolFailureReason.ToolFormatError),
            ToolFailureReason.TransientNetworkFailure => nameof(ToolFailureReason.TransientNetworkFailure),
            ToolFailureReason.FatalExecutionError => nameof(ToolFailureReason.FatalExecutionError),
            _ => nameof(ToolFailureReason.Unknown)
        };
    }

    /// <summary>
    /// 从字符串协议值解析为枚举。
    /// </summary>
    public static ToolFailureReason Parse(string? value)
    {
        var normalized = value?.Trim();

        return normalized switch
        {
            nameof(ToolFailureReason.None) => ToolFailureReason.None,
            nameof(ToolFailureReason.AuthExpired) => ToolFailureReason.AuthExpired,
            nameof(ToolFailureReason.AuthPermanent) => ToolFailureReason.AuthPermanent,
            nameof(ToolFailureReason.MissingUserInput) => ToolFailureReason.MissingUserInput,
            nameof(ToolFailureReason.ApprovalRequired) => ToolFailureReason.ApprovalRequired,
            nameof(ToolFailureReason.PermissionDenied) => ToolFailureReason.PermissionDenied,
            nameof(ToolFailureReason.PathNotFound) => ToolFailureReason.PathNotFound,
            nameof(ToolFailureReason.RateLimited) => ToolFailureReason.RateLimited,
            nameof(ToolFailureReason.BillingLimited) => ToolFailureReason.BillingLimited,
            nameof(ToolFailureReason.ContextOverflow) => ToolFailureReason.ContextOverflow,
            nameof(ToolFailureReason.ToolFormatError) => ToolFailureReason.ToolFormatError,
            nameof(ToolFailureReason.TransientNetworkFailure) => ToolFailureReason.TransientNetworkFailure,
            nameof(ToolFailureReason.FatalExecutionError) => ToolFailureReason.FatalExecutionError,
            _ => ToolFailureReason.Unknown
        };
    }
}
