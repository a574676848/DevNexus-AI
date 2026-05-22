using DevNexus.Domain.Entities;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Cli;

/// <summary>
/// CLI 回滚结果构建器。
/// </summary>
public static class CliRollbackResultBuilder
{
    private const string DefaultRuntimeHost = "process-cli";

    /// <summary>
    /// 构建运行中阻断回滚的结果。
    /// </summary>
    public static CliExecRollbackResultDto BuildBlockedByActiveSession(
        Guid sessionId,
        CliSessionStateDto activeState)
    {
        return new CliExecRollbackResultDto
        {
            SessionId = sessionId,
            RolledBack = false,
            Message = "终端仍在执行，不能在运行中回滚。",
            WorkingDirectory = activeState.WorkingDirectory,
            State = activeState
        };
    }

    /// <summary>
    /// 构建回滚成功后的结果。
    /// </summary>
    public static CliExecRollbackResultDto BuildRolledBack(
        Guid sessionId,
        string sessionKey,
        CliExecRollbackResultDto checkpointResult,
        CliExecSession? existingSession)
    {
        var state = BuildRolledBackState(sessionId, sessionKey, checkpointResult, existingSession);
        return new CliExecRollbackResultDto
        {
            SessionId = sessionId,
            RolledBack = true,
            Message = checkpointResult.Message,
            WorkingDirectory = checkpointResult.WorkingDirectory,
            State = state
        };
    }

    /// <summary>
    /// 构建回滚后的持久化会话事实。
    /// </summary>
    public static CliExecSession BuildPersistedSession(
        Guid userId,
        CliSessionStateDto rolledBackState)
    {
        return new CliExecSession
        {
            SessionKey = rolledBackState.SessionKey,
            UserId = userId,
            ChatSessionId = rolledBackState.SessionId,
            ExecStatus = rolledBackState.ExecStatus,
            SessionMode = rolledBackState.SessionMode,
            Command = rolledBackState.Command,
            WorkingDirectory = rolledBackState.WorkingDirectory,
            RuntimeHost = rolledBackState.RuntimeHost,
            TerminalStreamId = rolledBackState.TerminalStreamId,
            StartedAt = rolledBackState.StartedAt,
            LastActivityAt = rolledBackState.LastActivityAt,
            WaitingForInput = rolledBackState.WaitingForInput,
            WaitingForInputSince = rolledBackState.WaitingForInputSince,
            ExitCode = null,
            TerminationReason = rolledBackState.TerminationReason,
            IsActive = rolledBackState.IsActive
        };
    }

    private static CliSessionStateDto BuildRolledBackState(
        Guid sessionId,
        string sessionKey,
        CliExecRollbackResultDto checkpointResult,
        CliExecSession? existingSession)
    {
        return new CliSessionStateDto
        {
            SessionId = sessionId,
            ExecStatus = CliExecStatus.RolledBack,
            SessionMode = existingSession?.SessionMode ?? CliSessionMode.InteractiveShell,
            SessionKey = sessionKey,
            TerminalStreamId = existingSession?.TerminalStreamId,
            Command = existingSession?.Command ?? string.Empty,
            WorkingDirectory = checkpointResult.WorkingDirectory,
            Status = TerminalStreamStatus.Completed.ToWireValue(),
            SessionState = CliSessionState.RolledBack.ToWireValue(),
            RuntimeHost = string.IsNullOrWhiteSpace(existingSession?.RuntimeHost)
                ? DefaultRuntimeHost
                : existingSession.RuntimeHost,
            StartedAt = existingSession?.StartedAt ?? DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            WaitingForInput = false,
            WaitingForInputSince = null,
            TerminationReason = CliSessionTerminationReasons.Completed,
            IsActive = false,
            StatusSummary = CliRuntimeStatusSummaryBuilder.Build(
                CliExecStatus.RolledBack,
                waitingForInput: false,
                CliSessionTerminationReasons.Completed)
        };
    }
}
