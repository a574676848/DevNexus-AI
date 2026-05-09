using DevNexus.Domain.Entities.Base;
using DevNexus.Shared.Enums;

namespace DevNexus.Domain.Entities;

/// <summary>
/// CLI 执行会话实体。
/// 用于持久化命令执行会话的统一运行状态。
/// </summary>
public class CliExecSession : AuditableEntity
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
    /// 统一执行状态。
    /// </summary>
    public CliExecStatus ExecStatus { get; set; } = CliExecStatus.Unknown;

    /// <summary>
    /// 会话模式。
    /// </summary>
    public CliSessionMode SessionMode { get; set; } = CliSessionMode.Unknown;

    /// <summary>
    /// 最近一次命令。
    /// </summary>
    public string? Command { get; set; }

    /// <summary>
    /// 工作目录。
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// 运行时宿主。
    /// </summary>
    public string? RuntimeHost { get; set; }

    /// <summary>
    /// 最近关联的终端流标识。
    /// </summary>
    public Guid? TerminalStreamId { get; set; }

    /// <summary>
    /// 启动时间。
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// 最近活动时间。
    /// </summary>
    public DateTime? LastActivityAt { get; set; }

    /// <summary>
    /// 是否等待输入。
    /// </summary>
    public bool WaitingForInput { get; set; }

    /// <summary>
    /// 等待输入开始时间。
    /// </summary>
    public DateTime? WaitingForInputSince { get; set; }

    /// <summary>
    /// 最近退出码。
    /// </summary>
    public int? ExitCode { get; set; }

    /// <summary>
    /// 终止原因。
    /// </summary>
    public string? TerminationReason { get; set; }

    /// <summary>
    /// 是否仍为活动会话。
    /// </summary>
    public bool IsActive { get; set; }
}
