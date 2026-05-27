namespace DevNexus.Shared.DTOs;

using System.Text.Json.Serialization;
using DevNexus.Shared.Enums;

/// <summary>
/// 挂起交互解决请求。
/// </summary>
public class PendingInteractionResolutionRequest
{
    /// <summary>
    /// 解决动作。
    /// </summary>
    public string Action { get; set; } = "submit";

    /// <summary>
    /// 用户提交的字段值。
    /// </summary>
    public Dictionary<string, string?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// 挂起交互解决响应。
/// </summary>
public class PendingInteractionResolutionResponse
{
    /// <summary>
    /// 已解决的交互标识。
    /// </summary>
    public Guid InteractionId { get; set; }

    /// <summary>
    /// 是否继续执行。
    /// </summary>
    public bool ShouldResume { get; set; }

    /// <summary>
    /// 归一后的解决动作。
    /// </summary>
    public string Action { get; set; } = "submit";

    /// <summary>
    /// CLI 审批授权范围。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CliApprovalGrantScope? ApprovalScope { get; set; }

}
