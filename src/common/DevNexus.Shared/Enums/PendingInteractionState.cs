namespace DevNexus.Shared.Enums;

/// <summary>
/// 挂起交互类型。
/// </summary>
public enum PendingInteractionKind
{
    /// <summary>
    /// 未知。
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 等待审批。
    /// </summary>
    Approval = 1,

    /// <summary>
    /// 等待凭证。
    /// </summary>
    Credential = 2,

    /// <summary>
    /// 等待澄清。
    /// </summary>
    Clarification = 3,

    /// <summary>
    /// 等待确认。
    /// </summary>
    Confirmation = 4,

    /// <summary>
    /// 等待外部授权回调。
    /// </summary>
    OAuthCallback = 5
}

/// <summary>
/// 挂起交互状态。
/// </summary>
public enum PendingInteractionStatus
{
    /// <summary>
    /// 未知状态。
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 等待中。
    /// </summary>
    Pending = 1,

    /// <summary>
    /// 已解决。
    /// </summary>
    Resolved = 2,

    /// <summary>
    /// 已过期。
    /// </summary>
    Expired = 3,

    /// <summary>
    /// 已取消。
    /// </summary>
    Cancelled = 4
}

/// <summary>
/// 挂起交互类型字符串协议扩展。
/// </summary>
public static class PendingInteractionKindExtensions
{
    /// <summary>
    /// 转换为前后端传输使用的字符串值。
    /// </summary>
    public static string ToWireValue(this PendingInteractionKind kind)
    {
        return kind switch
        {
            PendingInteractionKind.Approval => nameof(PendingInteractionKind.Approval),
            PendingInteractionKind.Credential => nameof(PendingInteractionKind.Credential),
            PendingInteractionKind.Clarification => nameof(PendingInteractionKind.Clarification),
            PendingInteractionKind.Confirmation => nameof(PendingInteractionKind.Confirmation),
            PendingInteractionKind.OAuthCallback => nameof(PendingInteractionKind.OAuthCallback),
            _ => nameof(PendingInteractionKind.Unknown)
        };
    }

    /// <summary>
    /// 从字符串协议值解析为枚举。
    /// </summary>
    public static PendingInteractionKind Parse(string? value)
    {
        var normalized = value?.Trim();

        return normalized switch
        {
            nameof(PendingInteractionKind.Approval) => PendingInteractionKind.Approval,
            nameof(PendingInteractionKind.Credential) => PendingInteractionKind.Credential,
            nameof(PendingInteractionKind.Clarification) => PendingInteractionKind.Clarification,
            nameof(PendingInteractionKind.Confirmation) => PendingInteractionKind.Confirmation,
            nameof(PendingInteractionKind.OAuthCallback) => PendingInteractionKind.OAuthCallback,
            _ => PendingInteractionKind.Unknown
        };
    }
}

/// <summary>
/// 挂起交互状态字符串协议扩展。
/// </summary>
public static class PendingInteractionStatusExtensions
{
    /// <summary>
    /// 转换为前后端传输使用的字符串值。
    /// </summary>
    public static string ToWireValue(this PendingInteractionStatus status)
    {
        return status switch
        {
            PendingInteractionStatus.Pending => nameof(PendingInteractionStatus.Pending),
            PendingInteractionStatus.Resolved => nameof(PendingInteractionStatus.Resolved),
            PendingInteractionStatus.Expired => nameof(PendingInteractionStatus.Expired),
            PendingInteractionStatus.Cancelled => nameof(PendingInteractionStatus.Cancelled),
            _ => nameof(PendingInteractionStatus.Unknown)
        };
    }

    /// <summary>
    /// 从字符串协议值解析为枚举。
    /// </summary>
    public static PendingInteractionStatus Parse(string? value)
    {
        var normalized = value?.Trim();

        return normalized switch
        {
            nameof(PendingInteractionStatus.Pending) => PendingInteractionStatus.Pending,
            nameof(PendingInteractionStatus.Resolved) => PendingInteractionStatus.Resolved,
            nameof(PendingInteractionStatus.Expired) => PendingInteractionStatus.Expired,
            nameof(PendingInteractionStatus.Cancelled) => PendingInteractionStatus.Cancelled,
            _ => PendingInteractionStatus.Unknown
        };
    }
}
