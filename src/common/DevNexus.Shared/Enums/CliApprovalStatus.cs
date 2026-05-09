using System.Text.Json.Serialization;

namespace DevNexus.Shared.Enums;

/// <summary>
/// CLI 审批请求的统一状态。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CliApprovalStatus
{
    /// <summary>
    /// 未知状态。
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 等待审批。
    /// </summary>
    Pending = 1,

    /// <summary>
    /// 已批准。
    /// </summary>
    Approved = 2,

    /// <summary>
    /// 已拒绝。
    /// </summary>
    Rejected = 3,

    /// <summary>
    /// 已过期或失效。
    /// </summary>
    Expired = 4
}
