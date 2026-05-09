using System.Text.Json.Serialization;
using DevNexus.Shared.Enums;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// CLI 执行快照 DTO。
/// </summary>
public sealed class CliExecCheckpointDto
{
    /// <summary>
    /// 快照标识。
    /// </summary>
    public Guid CheckpointId { get; set; }

    /// <summary>
    /// 聊天会话标识。
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 内部会话键。
    /// </summary>
    public string SessionKey { get; set; } = string.Empty;

    /// <summary>
    /// 触发快照的命令。
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// 工作目录。
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>
    /// 快照状态。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CliExecCheckpointStatus Status { get; set; } = CliExecCheckpointStatus.Created;

    /// <summary>
    /// 创建时间。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 最近更新时间。
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 回滚时间。
    /// </summary>
    public DateTime? RolledBackAt { get; set; }
}
