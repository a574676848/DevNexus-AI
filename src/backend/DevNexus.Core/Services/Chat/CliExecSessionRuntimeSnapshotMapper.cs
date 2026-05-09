using DevNexus.Core.Models.Cli;
using DevNexus.Domain.Entities;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// CLI 执行会话实体到运行时快照的映射辅助。
/// </summary>
internal static class CliExecSessionRuntimeSnapshotMapper
{
    /// <summary>
    /// 将持久化执行会话映射为运行时快照。
    /// </summary>
    public static CliSessionRuntimeSnapshot ToRuntimeSnapshot(CliExecSession session)
    {
        return new CliSessionRuntimeSnapshot(
            session.SessionKey,
            session.WorkingDirectory ?? string.Empty,
            string.Empty,
            session.StartedAt ?? session.CreatedAt,
            session.LastActivityAt ?? session.UpdatedAt,
            session.WaitingForInput,
            session.WaitingForInputSince,
            ToExecutionState(session.ExecStatus),
            Enum.TryParse<CliSessionTerminationReason>(session.TerminationReason, true, out var parsed)
                ? parsed
                : CliSessionTerminationReason.None);
    }

    private static CliSessionExecutionState ToExecutionState(CliExecStatus status)
    {
        return status switch
        {
            CliExecStatus.Requested => CliSessionExecutionState.Created,
            CliExecStatus.Queued => CliSessionExecutionState.Created,
            CliExecStatus.Running => CliSessionExecutionState.Running,
            CliExecStatus.WaitingForInput => CliSessionExecutionState.WaitingForInput,
            CliExecStatus.Completed => CliSessionExecutionState.Completed,
            CliExecStatus.RolledBack => CliSessionExecutionState.Completed,
            CliExecStatus.Cancelled => CliSessionExecutionState.Cancelled,
            CliExecStatus.TimedOut => CliSessionExecutionState.TimedOut,
            CliExecStatus.Reaped => CliSessionExecutionState.Reaped,
            CliExecStatus.Failed => CliSessionExecutionState.Failed,
            _ => CliSessionExecutionState.Created
        };
    }
}
