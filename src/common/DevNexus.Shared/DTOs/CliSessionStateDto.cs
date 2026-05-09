using System.Text.Json.Serialization;
using DevNexus.Shared.Enums;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// CLI 会话状态 DTO。
/// </summary>
public sealed class CliSessionStateDto
{
    /// <summary>
    /// 聊天会话 ID。
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 统一执行状态。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CliExecStatus ExecStatus { get; set; } = CliExecStatus.Unknown;

    /// <summary>
    /// 会话模式。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CliSessionMode SessionMode { get; set; } = CliSessionMode.Unknown;

    /// <summary>
    /// CLI 内部会话键。
    /// </summary>
    public string SessionKey { get; set; } = string.Empty;

    /// <summary>
    /// 终端流标识。
    /// </summary>
    public Guid? TerminalStreamId { get; set; }

    /// <summary>
    /// 命令文本。
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// 工作目录。
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// 运行状态。
    /// </summary>
    public string Status { get; set; } = "Created";

    /// <summary>
    /// 会话状态。
    /// </summary>
    public string SessionState { get; set; } = "Created";

    /// <summary>
    /// 运行时宿主。
    /// </summary>
    public string? RuntimeHost { get; set; }

    /// <summary>
    /// 是否等待输入。
    /// </summary>
    public bool WaitingForInput { get; set; }

    /// <summary>
    /// 等待输入开始时间。
    /// </summary>
    public DateTime? WaitingForInputSince { get; set; }

    /// <summary>
    /// 启动时间。
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// 最近活动时间。
    /// </summary>
    public DateTime? LastActivityAt { get; set; }

    /// <summary>
    /// 终止原因。
    /// </summary>
    public string? TerminationReason { get; set; }

    /// <summary>
    /// 是否仍为活动会话。
    /// </summary>
    public bool IsActive { get; set; }
}
