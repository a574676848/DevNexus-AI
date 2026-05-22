using DevNexus.Core.Models.Cli;
using DevNexus.Domain.Entities;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Cli;

/// <summary>
/// CLI 运行时 DTO 映射器。
/// </summary>
public static class CliRuntimeDtoMapper
{
    /// <summary>
    /// 将运行时快照映射为统一会话状态 DTO。
    /// </summary>
    public static CliSessionStateDto ToSessionState(Guid sessionId, CliSessionRuntimeSnapshot snapshot)
    {
        var sessionState = ToSessionState(snapshot.State);
        var execStatus = ToExecStatus(snapshot.State);
        return new CliSessionStateDto
        {
            SessionId = sessionId,
            ExecStatus = execStatus,
            SessionMode = CliSessionMode.InteractiveShell,
            SessionKey = snapshot.SessionKey,
            Command = string.Empty,
            WorkingDirectory = snapshot.WorkingDirectory,
            Status = ToTerminalStatus(snapshot.State).ToWireValue(),
            SessionState = sessionState.ToWireValue(),
            RuntimeHost = "process-cli",
            WaitingForInput = snapshot.WaitingForInput,
            WaitingForInputSince = snapshot.WaitingForInputSince,
            StartedAt = snapshot.StartedAt,
            LastActivityAt = snapshot.LastActivityAt,
            TerminationReason = CliSessionTerminationReasons.Normalize(snapshot.TerminationReason.ToString(), string.Empty),
            IsActive = sessionState.IsActive(),
            StatusSummary = CliRuntimeStatusSummaryBuilder.Build(
                execStatus,
                snapshot.WaitingForInput,
                snapshot.TerminationReason.ToString())
        };
    }

    /// <summary>
    /// 将持久化实体映射为统一会话状态 DTO。
    /// </summary>
    public static CliSessionStateDto ToSessionState(CliExecSession session)
    {
        return new CliSessionStateDto
        {
            SessionId = session.ChatSessionId ?? Guid.Empty,
            ExecStatus = session.ExecStatus,
            SessionMode = session.SessionMode,
            SessionKey = session.SessionKey,
            TerminalStreamId = session.TerminalStreamId,
            Command = session.Command ?? string.Empty,
            WorkingDirectory = session.WorkingDirectory,
            Status = session.ExecStatus switch
            {
                CliExecStatus.PendingApproval => TerminalStreamStatus.Running.ToWireValue(),
                CliExecStatus.Completed => TerminalStreamStatus.Completed.ToWireValue(),
                CliExecStatus.RolledBack => TerminalStreamStatus.Completed.ToWireValue(),
                CliExecStatus.Failed or CliExecStatus.Cancelled or CliExecStatus.Reaped or CliExecStatus.TimedOut
                    => TerminalStreamStatus.Failed.ToWireValue(),
                _ => TerminalStreamStatus.Running.ToWireValue()
            },
            SessionState = ToSessionState(session.ExecStatus).ToWireValue(),
            RuntimeHost = session.RuntimeHost,
            WaitingForInput = session.WaitingForInput,
            WaitingForInputSince = session.WaitingForInputSince,
            StartedAt = session.StartedAt,
            LastActivityAt = session.LastActivityAt,
            TerminationReason = session.TerminationReason,
            IsActive = session.IsActive,
            StatusSummary = CliRuntimeStatusSummaryBuilder.Build(
                session.ExecStatus,
                session.WaitingForInput,
                session.TerminationReason)
        };
    }

    /// <summary>
    /// 映射快照 DTO。
    /// </summary>
    public static CliExecCheckpointDto ToCheckpointDto(CliExecCheckpoint checkpoint, Guid sessionId)
    {
        return new CliExecCheckpointDto
        {
            CheckpointId = checkpoint.Id,
            SessionId = sessionId,
            SessionKey = checkpoint.SessionKey,
            Command = checkpoint.Command,
            WorkingDirectory = checkpoint.WorkingDirectory,
            Status = checkpoint.Status,
            CreatedAt = checkpoint.CreatedAt,
            UpdatedAt = checkpoint.UpdatedAt,
            RolledBackAt = checkpoint.RolledBackAt
        };
    }

    private static CliExecStatus ToExecStatus(CliSessionExecutionState state)
    {
        return state switch
        {
            CliSessionExecutionState.Created => CliExecStatus.Queued,
            CliSessionExecutionState.Running => CliExecStatus.Running,
            CliSessionExecutionState.WaitingForInput => CliExecStatus.WaitingForInput,
            CliSessionExecutionState.Completed => CliExecStatus.Completed,
            CliSessionExecutionState.Cancelled => CliExecStatus.Cancelled,
            CliSessionExecutionState.TimedOut => CliExecStatus.TimedOut,
            CliSessionExecutionState.Reaped => CliExecStatus.Reaped,
            CliSessionExecutionState.Failed => CliExecStatus.Failed,
            _ => CliExecStatus.Unknown
        };
    }

    private static TerminalStreamStatus ToTerminalStatus(CliSessionExecutionState state)
    {
        return state switch
        {
            CliSessionExecutionState.Completed => TerminalStreamStatus.Completed,
            CliSessionExecutionState.Cancelled or CliSessionExecutionState.Failed
                or CliSessionExecutionState.TimedOut or CliSessionExecutionState.Reaped
                => TerminalStreamStatus.Failed,
            _ => TerminalStreamStatus.Running
        };
    }

    /// <summary>
    /// 将执行态映射为会话态。
    /// </summary>
    public static CliSessionState ToSessionState(CliExecStatus status)
    {
        return status switch
        {
            CliExecStatus.Requested or CliExecStatus.PendingApproval => CliSessionState.Created,
            CliExecStatus.Queued => CliSessionState.Queued,
            CliExecStatus.Running => CliSessionState.Running,
            CliExecStatus.WaitingForInput => CliSessionState.WaitingForInput,
            CliExecStatus.Completed => CliSessionState.Completed,
            CliExecStatus.Failed => CliSessionState.Failed,
            CliExecStatus.Cancelled => CliSessionState.Cancelled,
            CliExecStatus.TimedOut => CliSessionState.TimedOut,
            CliExecStatus.Reaped => CliSessionState.Reaped,
            CliExecStatus.RolledBack => CliSessionState.RolledBack,
            _ => CliSessionState.Unknown
        };
    }

    private static CliSessionState ToSessionState(CliSessionExecutionState state)
    {
        return state switch
        {
            CliSessionExecutionState.Created => CliSessionState.Queued,
            CliSessionExecutionState.Running => CliSessionState.Running,
            CliSessionExecutionState.WaitingForInput => CliSessionState.WaitingForInput,
            CliSessionExecutionState.Completed => CliSessionState.Completed,
            CliSessionExecutionState.Cancelled => CliSessionState.Cancelled,
            CliSessionExecutionState.TimedOut => CliSessionState.TimedOut,
            CliSessionExecutionState.Reaped => CliSessionState.Reaped,
            CliSessionExecutionState.Failed => CliSessionState.Failed,
            _ => CliSessionState.Unknown
        };
    }
}
