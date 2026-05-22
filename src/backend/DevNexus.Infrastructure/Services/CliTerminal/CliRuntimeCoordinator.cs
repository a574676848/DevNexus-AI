using DevNexus.Core.Abstractions;
using DevNexus.Core.Models.Cli;
using DevNexus.Core.Services.Cli;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Infrastructure.Services.CliTerminal;

/// <summary>
/// CLI 运行时协调器实现。
/// </summary>
public sealed class CliRuntimeCoordinator : ICliRuntimeCoordinator
{
    private readonly ICliProcessRegistry _cliProcessRegistry;
    private readonly ICliExecSessionRepository _cliExecSessionRepository;
    private readonly ICliExecCheckpointRepository _cliExecCheckpointRepository;
    private readonly ICliExecCheckpointService _cliExecCheckpointService;
    private readonly ITerminalOutputBuffer _terminalOutputBuffer;
    private readonly IRuntimeEventNotifier _runtimeEventNotifier;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public CliRuntimeCoordinator(
        ICliProcessRegistry cliProcessRegistry,
        ICliExecSessionRepository cliExecSessionRepository,
        ICliExecCheckpointRepository cliExecCheckpointRepository,
        ICliExecCheckpointService cliExecCheckpointService,
        ITerminalOutputBuffer terminalOutputBuffer,
        IRuntimeEventNotifier runtimeEventNotifier)
    {
        _cliProcessRegistry = cliProcessRegistry;
        _cliExecSessionRepository = cliExecSessionRepository;
        _cliExecCheckpointRepository = cliExecCheckpointRepository;
        _cliExecCheckpointService = cliExecCheckpointService;
        _terminalOutputBuffer = terminalOutputBuffer;
        _runtimeEventNotifier = runtimeEventNotifier;
    }

    /// <inheritdoc />
    public async Task<CliExecSessionDto?> GetSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var sessionKey = BuildSessionKey(userId, sessionId);
        var runtimeSnapshot = _cliProcessRegistry.GetRuntimeSnapshot(sessionKey);
        var persistedSession = await _cliExecSessionRepository.GetBySessionKeyAsync(sessionKey, cancellationToken);
        CliSessionStateDto? state = runtimeSnapshot == null
            ? null
            : CliRuntimeDtoMapper.ToSessionState(sessionId, runtimeSnapshot);

        if (state != null && persistedSession != null)
        {
            state.Command = string.IsNullOrWhiteSpace(persistedSession.Command) ? state.Command : persistedSession.Command;
            state.RuntimeHost = string.IsNullOrWhiteSpace(persistedSession.RuntimeHost) ? state.RuntimeHost : persistedSession.RuntimeHost;
            state.TerminalStreamId ??= persistedSession.TerminalStreamId;
        }

        if (state == null && persistedSession != null)
        {
            state = CliRuntimeDtoMapper.ToSessionState(persistedSession);
            state.SessionId = sessionId;
        }

        if (state == null)
        {
            return null;
        }

        var archivedOutput = await ReadArchivedOutputAsync(state, cancellationToken);
        var liveOutput = _cliProcessRegistry.GetStrippedOutput(state.SessionKey);

        return new CliExecSessionDto
        {
            SessionId = sessionId,
            State = state,
            OutputTail = ResolveOutputTail(liveOutput, archivedOutput?.Content),
            OutputLength = Math.Max(liveOutput.Length, archivedOutput?.OutputLength ?? 0),
            Exited = !state.IsActive,
            LatestCheckpoint = await GetLatestCheckpointAsync(userId, sessionId, cancellationToken)
        };
    }

    /// <inheritdoc />
    public async Task<CliExecLogChunkDto?> GetLogChunkAsync(
        Guid userId,
        Guid sessionId,
        int startIndex = 0,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionAsync(userId, sessionId, cancellationToken);
        if (session?.State == null)
        {
            return null;
        }

        var archivedOutput = await ReadArchivedOutputAsync(session.State, cancellationToken);
        var liveRawOutput = _cliProcessRegistry.GetRawOutput(session.State.SessionKey);
        var livePlainOutput = _cliProcessRegistry.GetStrippedOutput(session.State.SessionKey);
        var useArchivedOutput = !session.State.IsActive
            && archivedOutput != null
            && archivedOutput.OutputLength >= livePlainOutput.Length;
        var effectiveOutput = useArchivedOutput ? archivedOutput!.Content : _cliProcessRegistry.GetRawOutput(session.State.SessionKey, startIndex);
        var effectivePlainOutput = useArchivedOutput ? archivedOutput!.Content : _cliProcessRegistry.GetStrippedOutput(session.State.SessionKey, startIndex);
        var slicedOutput = startIndex <= 0 || startIndex >= effectiveOutput.Length
            ? (startIndex <= 0 ? effectiveOutput : string.Empty)
            : effectiveOutput[startIndex..];
        var slicedPlainOutput = startIndex <= 0 || startIndex >= effectivePlainOutput.Length
            ? (startIndex <= 0 ? effectivePlainOutput : string.Empty)
            : effectivePlainOutput[startIndex..];

        return new CliExecLogChunkDto
        {
            SessionId = sessionId,
            SessionKey = session.State.SessionKey,
            Output = slicedOutput,
            PlainOutput = slicedPlainOutput,
            StartIndex = startIndex,
            OutputLength = useArchivedOutput
                ? archivedOutput!.OutputLength
                : Math.Max(liveRawOutput.Length, livePlainOutput.Length),
            HasArchivedOutput = archivedOutput?.HasArchivedOutput ?? false,
            WatchSummary = archivedOutput?.WatchSummary
        };
    }

    /// <inheritdoc />
    public async Task<CliExecSessionDto?> WaitForExitAsync(
        Guid userId,
        Guid sessionId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var sessionKey = BuildSessionKey(userId, sessionId);
        var snapshot = await _cliProcessRegistry.WaitForExitAsync(sessionKey, timeout, cancellationToken);
        if (snapshot == null)
        {
            return await GetSessionAsync(userId, sessionId, cancellationToken);
        }
        return await GetSessionAsync(userId, sessionId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CliExecSessionDto> WriteInputAsync(
        Guid userId,
        Guid sessionId,
        string input,
        CancellationToken cancellationToken = default)
    {
        var sessionKey = BuildSessionKey(userId, sessionId);
        var currentSession = await GetSessionAsync(userId, sessionId, cancellationToken);
        if (currentSession?.State == null)
        {
            return new CliExecSessionDto { SessionId = sessionId };
        }

        if (!currentSession.State.IsActive)
        {
            return currentSession;
        }

        var inputEnvelope = CliRuntimeInputProtocol.Build(input);
        await _cliProcessRegistry.WriteAsync(sessionKey, inputEnvelope.Input, cancellationToken);

        var session = await GetSessionAsync(userId, sessionId, cancellationToken)
            ?? currentSession;

        if (session.State != null)
        {
            await _runtimeEventNotifier.NotifyAsync(
                userId,
                sessionId,
                ResolveEventType(session.State),
                session.State,
                cancellationToken);
        }

        if (session.State != null && string.IsNullOrWhiteSpace(session.State.Command))
        {
            session.State.Command = inputEnvelope.ModelVisiblePreview;
        }

        return session;
    }

    /// <inheritdoc />
    public async Task<CliExecTerminateResultDto> TerminateAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var sessionKey = BuildSessionKey(userId, sessionId);
        var currentSession = await GetSessionAsync(userId, sessionId, cancellationToken);
        if (currentSession?.State == null)
        {
            return CliTerminationResultBuilder.BuildMissing(sessionId);
        }

        if (!currentSession.State.IsActive)
        {
            return CliTerminationResultBuilder.BuildAlreadyExited(sessionId, currentSession.State);
        }

        _cliProcessRegistry.MarkSessionTerminated(
            sessionKey,
            CliSessionExecutionState.Cancelled,
            CliSessionTerminationReason.Cancelled);
        _cliProcessRegistry.TerminateSession(sessionKey);
        _cliProcessRegistry.CleanupBuffers(sessionKey);

        var result = CliTerminationResultBuilder.BuildTerminated(sessionId, currentSession.State);
        if (result.State != null)
        {
            await _cliExecSessionRepository.UpsertAsync(
                CliTerminationResultBuilder.BuildPersistedSession(userId, result.State),
                cancellationToken);
        }

        await _runtimeEventNotifier.NotifyAsync(
            userId,
            sessionId,
            ServerEventType.CliExecCancelled,
            result.State,
            cancellationToken);

        return result;
    }

    /// <inheritdoc />
    public async Task<CliExecRollbackResultDto> RollbackAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var currentSession = await GetSessionAsync(userId, sessionId, cancellationToken);
        if (currentSession?.State?.IsActive == true)
        {
            return CliRollbackResultBuilder.BuildBlockedByActiveSession(sessionId, currentSession.State);
        }

        var sessionKey = BuildSessionKey(userId, sessionId);
        var result = await _cliExecCheckpointService.RollbackLatestAsync(
            userId,
            sessionId,
            sessionKey,
            cancellationToken);

        if (result.RolledBack)
        {
            _cliProcessRegistry.CleanupBuffers(sessionKey);
            var existing = await _cliExecSessionRepository.GetBySessionKeyAsync(sessionKey, cancellationToken);
            result = CliRollbackResultBuilder.BuildRolledBack(sessionId, sessionKey, result, existing);
            await _cliExecSessionRepository.UpsertAsync(
                CliRollbackResultBuilder.BuildPersistedSession(userId, result.State!),
                cancellationToken);
            await _runtimeEventNotifier.NotifyAsync(
                userId,
                sessionId,
                ServerEventType.CliExecRolledBack,
                result,
                cancellationToken);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<CliSessionRuntimeSnapshot?> GetRuntimeSnapshotAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var sessionKey = BuildSessionKey(userId, sessionId);
        var runtimeSnapshot = _cliProcessRegistry.GetRuntimeSnapshot(sessionKey);
        if (runtimeSnapshot != null)
        {
            return runtimeSnapshot;
        }

        var persistedSession = await _cliExecSessionRepository.GetBySessionKeyAsync(sessionKey, cancellationToken);
        return persistedSession?.IsActive == true ? ToRuntimeSnapshot(persistedSession) : null;
    }

    /// <inheritdoc />
    public async Task<CliExecCheckpointDto?> GetLatestCheckpointAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var checkpoint = await _cliExecCheckpointRepository.GetLatestActiveBySessionKeyAsync(
            BuildSessionKey(userId, sessionId),
            cancellationToken);
        return checkpoint == null ? null : CliRuntimeDtoMapper.ToCheckpointDto(checkpoint, sessionId);
    }

    private static string BuildSessionKey(Guid userId, Guid sessionId)
    {
        return $"{userId:N}:{sessionId}";
    }

    private static CliExecSessionDto CreateFallbackSession(
        Guid sessionId,
        string sessionKey,
        CliSessionRuntimeSnapshot? snapshot)
    {
        var state = snapshot == null
            ? new CliSessionStateDto
            {
                SessionId = sessionId,
                ExecStatus = CliExecStatus.Queued,
                SessionMode = CliSessionMode.InteractiveShell,
                SessionKey = sessionKey,
                Status = TerminalStreamStatus.Running.ToWireValue(),
                SessionState = CliSessionState.Queued.ToWireValue(),
                StartedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                WaitingForInput = false,
                TerminationReason = CliSessionTerminationReasons.None,
                IsActive = true,
                StatusSummary = CliRuntimeStatusSummaryBuilder.Build(
                    CliExecStatus.Queued,
                    waitingForInput: false,
                    CliSessionTerminationReasons.None)
            }
            : CliRuntimeDtoMapper.ToSessionState(sessionId, snapshot);

        return new CliExecSessionDto
        {
            SessionId = sessionId,
            State = state,
            Exited = !state.IsActive
        };
    }

    private static ServerEventType ResolveEventType(CliSessionStateDto state)
    {
        return state.ExecStatus switch
        {
            CliExecStatus.WaitingForInput => ServerEventType.CliExecWaitingForInput,
            CliExecStatus.Completed => ServerEventType.CliExecCompleted,
            CliExecStatus.RolledBack => ServerEventType.CliExecRolledBack,
            CliExecStatus.Cancelled => ServerEventType.CliExecCancelled,
            CliExecStatus.TimedOut => ServerEventType.CliExecTimedOut,
            CliExecStatus.Failed or CliExecStatus.Reaped => ServerEventType.CliExecFailed,
            CliExecStatus.Running or CliExecStatus.Requested or CliExecStatus.Queued => ServerEventType.CliExecStarted,
            _ => ServerEventType.CliExecStarted
        };
    }

    private static CliSessionRuntimeSnapshot ToRuntimeSnapshot(CliExecSession session)
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
            CliExecStatus.Cancelled => CliSessionExecutionState.Cancelled,
            CliExecStatus.TimedOut => CliSessionExecutionState.TimedOut,
            CliExecStatus.Reaped => CliSessionExecutionState.Reaped,
            CliExecStatus.RolledBack => CliSessionExecutionState.Completed,
            CliExecStatus.Failed => CliSessionExecutionState.Failed,
            _ => CliSessionExecutionState.Created
        };
    }

    private async Task<TerminalOutputContentDto?> ReadArchivedOutputAsync(
        CliSessionStateDto state,
        CancellationToken cancellationToken)
    {
        if (!state.TerminalStreamId.HasValue || state.TerminalStreamId.Value == Guid.Empty)
        {
            return null;
        }

        return await _terminalOutputBuffer.ReadOutputAsync(state.TerminalStreamId.Value, cancellationToken);
    }

    private static string ResolveOutputTail(string liveOutput, string? archivedOutput)
    {
        var source = !string.IsNullOrWhiteSpace(liveOutput) ? liveOutput : archivedOutput ?? string.Empty;
        return source.Length <= 4000 ? source : source[^4000..];
    }
}
