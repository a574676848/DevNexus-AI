using DevNexus.Shared.Constants;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 终端记录 DTO，用于会话恢复时单独拉取活跃终端详情。
/// </summary>
public class TerminalRecordDto
{
    public Guid RecordId { get; set; }
    public Guid SessionId { get; set; }
    public Guid MessageId { get; set; }
    public Guid? TerminalStreamId { get; set; }
    public Guid? ToolCallId { get; set; }
    public string? PackageId { get; set; }
    public string Command { get; set; } = string.Empty;
    public string? WorkingDirectory { get; set; }
    public string Status { get; set; } = ChatTerminalProtocolDefaults.GetCompletedStatus();
    public string SessionState { get; set; } = ChatTerminalProtocolDefaults.GetCompletedSessionState();
    public string? RuntimeHost { get; set; }
    public int? ExitCode { get; set; }
    public int AttemptNumber { get; set; }
    public bool IsRetry { get; set; }
    public bool WaitingForInput { get; set; }
    public DateTime? WaitingForInputSince { get; set; }
    public string? TerminationReason { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public string Output { get; set; } = string.Empty;
    public bool HasArchivedOutput { get; set; }
    public int OutputLength { get; set; }
    public int OutputLineCount { get; set; }
    public string? WatchSummary { get; set; }
    public bool IsActive { get; set; }
}
