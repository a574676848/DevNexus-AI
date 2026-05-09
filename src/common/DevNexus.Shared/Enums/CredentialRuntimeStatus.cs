namespace DevNexus.Shared.Enums;

/// <summary>
/// 凭证运行时状态。
/// 用于统一描述当前凭证是否可用、将过期或已失效。
/// </summary>
public enum CredentialRuntimeStatus
{
    Unknown = 0,
    Ready = 1,
    ExpiringSoon = 2,
    Expired = 3,
    Invalid = 4,
    Inactive = 5,
    CoolingDown = 6
}
