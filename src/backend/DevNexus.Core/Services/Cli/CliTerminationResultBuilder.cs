using DevNexus.Domain.Entities;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Cli;

/// <summary>
/// CLI 终止结果构建器。
/// </summary>
public static class CliTerminationResultBuilder
{
    /// <summary>
    /// 构建会话缺失结果。
    /// </summary>
    public static CliExecTerminateResultDto BuildMissing(Guid sessionId)
    {
        return new CliExecTerminateResultDto
        {
            SessionId = sessionId,
            Terminated = false,
            AlreadyExited = true,
            Message = "当前终端会话不存在或已结束。"
        };
    }

    /// <summary>
    /// 构建会话已结束结果。
    /// </summary>
    public static CliExecTerminateResultDto BuildAlreadyExited(
        Guid sessionId,
        CliSessionStateDto state)
    {
        return new CliExecTerminateResultDto
        {
            SessionId = sessionId,
            Terminated = false,
            AlreadyExited = true,
            Message = "当前终端会话已结束。",
            State = state
        };
    }

    /// <summary>
    /// 构建终止成功结果。
    /// </summary>
    public static CliExecTerminateResultDto BuildTerminated(
        Guid sessionId,
        CliSessionStateDto previousState)
    {
        return new CliExecTerminateResultDto
        {
            SessionId = sessionId,
            Terminated = true,
            AlreadyExited = false,
            Message = "已停止当前终端会话。",
            State = BuildTerminatedState(sessionId, previousState)
        };
    }

    /// <summary>
    /// 构建终止后的持久化会话事实。
    /// </summary>
    public static CliExecSession BuildPersistedSession(
        Guid userId,
        CliSessionStateDto terminatedState)
    {
        return new CliExecSession
        {
            SessionKey = terminatedState.SessionKey,
            UserId = userId,
            ChatSessionId = terminatedState.SessionId,
            ExecStatus = terminatedState.ExecStatus,
            SessionMode = terminatedState.SessionMode,
            Command = terminatedState.Command,
            WorkingDirectory = terminatedState.WorkingDirectory,
            RuntimeHost = terminatedState.RuntimeHost,
            TerminalStreamId = terminatedState.TerminalStreamId,
            StartedAt = terminatedState.StartedAt,
            LastActivityAt = terminatedState.LastActivityAt,
            WaitingForInput = terminatedState.WaitingForInput,
            WaitingForInputSince = terminatedState.WaitingForInputSince,
            TerminationReason = terminatedState.TerminationReason,
            IsActive = terminatedState.IsActive
        };
    }

    private static CliSessionStateDto BuildTerminatedState(
        Guid sessionId,
        CliSessionStateDto previousState)
    {
        return new CliSessionStateDto
        {
            SessionId = sessionId,
            ExecStatus = CliExecStatus.Cancelled,
            SessionMode = previousState.SessionMode == CliSessionMode.Unknown
                ? CliSessionMode.InteractiveShell
                : previousState.SessionMode,
            SessionKey = previousState.SessionKey,
            TerminalStreamId = previousState.TerminalStreamId,
            Command = previousState.Command,
            WorkingDirectory = previousState.WorkingDirectory,
            Status = TerminalStreamStatus.Failed.ToWireValue(),
            SessionState = CliSessionState.Cancelled.ToWireValue(),
            RuntimeHost = string.IsNullOrWhiteSpace(previousState.RuntimeHost)
                ? "process-cli"
                : previousState.RuntimeHost,
            StartedAt = previousState.StartedAt,
            LastActivityAt = DateTime.UtcNow,
            WaitingForInput = false,
            WaitingForInputSince = null,
            TerminationReason = CliSessionTerminationReasons.Cancelled,
            IsActive = false,
            StatusSummary = CliRuntimeStatusSummaryBuilder.Build(
                CliExecStatus.Cancelled,
                waitingForInput: false,
                CliSessionTerminationReasons.Cancelled)
        };
    }
}
