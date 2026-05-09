using System.Text.Json.Serialization;

namespace DevNexus.Shared.Enums;

/// <summary>
/// CLI 审批授权范围。
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CliApprovalGrantScope
{
    /// <summary>
    /// 单次授权。
    /// </summary>
    Once = 1,

    /// <summary>
    /// 会话内同类命令授权。
    /// </summary>
    Pattern = 2
}
