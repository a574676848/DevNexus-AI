using DevNexus.Domain.Entities.Base;
using DevNexus.Shared.Enums;

namespace DevNexus.Domain.Entities;

/// <summary>
/// CLI 执行快照实体。
/// </summary>
public class CliExecCheckpoint : AuditableEntity
{
    /// <summary>
    /// 聊天会话标识。
    /// </summary>
    public Guid? ChatSessionId { get; set; }

    /// <summary>
    /// 用户标识。
    /// </summary>
    public Guid? UserId { get; set; }

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
    /// 快照目录。
    /// </summary>
    public string SnapshotDirectory { get; set; } = string.Empty;

    /// <summary>
    /// 快照状态。
    /// </summary>
    public CliExecCheckpointStatus Status { get; set; } = CliExecCheckpointStatus.Created;

    /// <summary>
    /// 回滚时间。
    /// </summary>
    public DateTime? RolledBackAt { get; set; }
}
