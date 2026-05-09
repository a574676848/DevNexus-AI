using System.Text.Json.Serialization;
using DevNexus.Shared.Enums;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// CLI 审批请求 DTO。
/// </summary>
public sealed class CliExecApprovalRequestDto
{
    /// <summary>
    /// 聊天会话标识。
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 内部 CLI 会话键。
    /// </summary>
    public string SessionKey { get; set; } = string.Empty;

    /// <summary>
    /// 关联的待处理交互标识。
    /// </summary>
    public Guid? InteractionId { get; set; }

    /// <summary>
    /// 待审批命令。
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// 工作目录。
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>
    /// 审批状态。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CliApprovalStatus Status { get; set; } = CliApprovalStatus.Pending;

    /// <summary>
    /// 失败原因。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ToolFailureReason FailureReason { get; set; } = ToolFailureReason.None;

    /// <summary>
    /// 建议动作。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ToolSuggestedAction SuggestedAction { get; set; } = ToolSuggestedAction.None;

    /// <summary>
    /// 面向前端或调用方的说明。
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
