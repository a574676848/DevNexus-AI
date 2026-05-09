using DevNexus.Domain.Entities.Base;
using DevNexus.Shared.Enums;

namespace DevNexus.Domain.Entities;

/// <summary>
/// 终端流实体 - 用于持久化终端执行输出
/// </summary>
public class TerminalStream : AuditableEntity
{
    /// <summary>
    /// CLI 会话键
    /// </summary>
    public string? SessionKey { get; set; }

    /// <summary>
    /// 关联的聊天会话ID
    /// </summary>
    public Guid? ChatSessionId { get; set; }

    /// <summary>
    /// 关联的用户ID
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// 关联的消息ID
    /// </summary>
    public Guid? MessageId { get; set; }

    /// <summary>
    /// 关联的消息
    /// </summary>
    public ChatMessage? Message { get; set; }

    /// <summary>
    /// 工具调用ID（可选）
    /// </summary>
    public Guid? ToolCallId { get; set; }

    /// <summary>
    /// 关联的工作包 ID。
    /// </summary>
    public string? PackageId { get; set; }

    /// <summary>
    /// 执行的命令
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// 工作目录
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// 工作目录锁键
    /// </summary>
    public string? LockKey { get; set; }

    /// <summary>
    /// 尝试次数（用于 Agent Loop 重试场景）
    /// </summary>
    public int AttemptNumber { get; set; } = 1;

    /// <summary>
    /// 是否为重试
    /// </summary>
    public bool IsRetry { get; set; } = false;

    /// <summary>
    /// 执行状态。
    /// </summary>
    public TerminalStreamStatus Status { get; set; } = TerminalStreamStatus.Running;

    /// <summary>
    /// CLI 会话状态
    /// </summary>
    public CliSessionState SessionState { get; set; } = CliSessionState.Created;

    /// <summary>
    /// 运行时宿主类型
    /// </summary>
    public string? RuntimeHost { get; set; }

    /// <summary>
    /// 退出码（执行完成后设置）
    /// </summary>
    public int? ExitCode { get; set; }

    /// <summary>
    /// 启动时间
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最后活动时间
    /// </summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 是否处于等待输入状态
    /// </summary>
    public bool WaitingForInput { get; set; }

    /// <summary>
    /// 等待输入开始时间
    /// </summary>
    public DateTime? WaitingForInputSince { get; set; }

    /// <summary>
    /// 终止原因
    /// </summary>
    public string? TerminationReason { get; set; }

    /// <summary>
    /// 可直接展示的输出窗口。
    /// </summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>
    /// 是否存在归档输出。
    /// </summary>
    public bool HasArchivedOutput { get; set; }

    /// <summary>
    /// 归档输出文件路径。
    /// </summary>
    public string? ArchivedOutputPath { get; set; }

    /// <summary>
    /// 输出字符数。
    /// </summary>
    public int OutputLength { get; set; }

    /// <summary>
    /// 输出行数。
    /// </summary>
    public int OutputLineCount { get; set; }

    /// <summary>
    /// 输出块数。
    /// </summary>
    public int OutputChunkCount { get; set; }

    /// <summary>
    /// 输出观察摘要。
    /// </summary>
    public string? WatchSummary { get; set; }
}
