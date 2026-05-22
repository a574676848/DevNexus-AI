using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using DevNexus.Core.Abstractions;
using DevNexus.Core.Models.Cli;
using DevNexus.Core.Services.Cli;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.CliTerminal;

/// <summary>
/// 基于标准 Process 的 CLI 运行时宿主。
/// </summary>
public sealed partial class ProcessCliRuntimeHost : ICliProcessRegistry
{
    private readonly ILogger<ProcessCliRuntimeHost> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICliSandboxSessionProvider _sandboxSessionProvider;
    private readonly ConcurrentDictionary<string, Process> _sessions = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastAccessTimes = new();
    private readonly ConcurrentDictionary<string, CliSessionRuntimeState> _runtimeStates = new();
    private readonly ConcurrentDictionary<string, StringBuilder> _rawBuffers = new();
    private readonly ConcurrentDictionary<string, StringBuilder> _strippedBuffers = new();
    private readonly ConcurrentDictionary<string, WarmShellEntry> _warmShells = new(StringComparer.OrdinalIgnoreCase);

    private const int MaxBufferSize = 1024 * 1024;
    private static readonly TimeSpan WarmShellMaxAge = TimeSpan.FromMinutes(2);

    /// <inheritdoc />
    public event Action<string, string>? OnOutputReceived;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public ProcessCliRuntimeHost(
        ILogger<ProcessCliRuntimeHost> logger,
        IServiceScopeFactory scopeFactory,
        ICliSandboxSessionProvider sandboxSessionProvider)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _sandboxSessionProvider = sandboxSessionProvider;
    }

    /// <inheritdoc />
    public async Task<string> CreateSessionAsync(
        string sessionId,
        string workingDirectory,
        CancellationToken ct)
    {
        _lastAccessTimes[sessionId] = DateTime.UtcNow;

        if (_sessions.TryGetValue(sessionId, out var existingProcess))
        {
            if (_runtimeStates.TryGetValue(sessionId, out var existingState))
            {
                existingState.LastActivityAt = DateTime.UtcNow;
            }

            if (!existingProcess.HasExited)
            {
                _ = PersistRuntimeSessionAsync(sessionId);
                return sessionId;
            }

            CleanupBuffers(sessionId);
            _sessions.TryRemove(sessionId, out _);
        }

        var normalizedWorkingDirectory = Path.GetFullPath(workingDirectory);
        var warmShell = TryTakeWarmShell(normalizedWorkingDirectory);
        var lease = warmShell?.Lease
            ?? await _sandboxSessionProvider.AcquireAsync(sessionId, normalizedWorkingDirectory, ct);
        var process = warmShell?.Process ?? StartPersistentShell(lease, sessionId);
        _sessions[sessionId] = process;
        _rawBuffers[sessionId] = new StringBuilder();
        _strippedBuffers[sessionId] = new StringBuilder();
        _runtimeStates[sessionId] = new CliSessionRuntimeState
        {
            SessionKey = sessionId,
            WorkingDirectory = lease.WorkingDirectory,
            LockKey = lease.LockKey,
            LeaseSessionKey = lease.SessionId,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            State = CliSessionExecutionState.Created,
            TerminationReason = CliSessionTerminationReason.None
        };

        _logger.LogInformation(
            "已为会话 {Id} 创建持久化 Shell (工作目录: {Cwd} Warm={Warm})",
            sessionId,
            normalizedWorkingDirectory,
            warmShell != null);
        _ = PersistRuntimeSessionAsync(sessionId);

        _ = Task.Run(() => ReadLoop(sessionId, process), ct);

        return sessionId;
    }

    /// <inheritdoc />
    public async Task WriteAsync(string sessionId, string input, CancellationToken ct)
    {
        _lastAccessTimes[sessionId] = DateTime.UtcNow;
        if (_runtimeStates.TryGetValue(sessionId, out var runtimeState))
        {
            runtimeState.LastActivityAt = DateTime.UtcNow;
            runtimeState.WaitingForInput = false;
            runtimeState.WaitingForInputSince = null;
            runtimeState.State = CliSessionExecutionState.Running;
        }

        if (_sessions.TryGetValue(sessionId, out var process) && !process.HasExited)
        {
            await process.StandardInput.WriteLineAsync(input.AsMemory(), ct);
            await process.StandardInput.FlushAsync(ct);
        }

        await PersistRuntimeSessionAsync(sessionId, command: input);
    }

    /// <inheritdoc />
    public async Task<CliCommandExecutionResult> ExecuteAndWaitAsync(
        string sessionId,
        string command,
        TimeSpan timeout,
        CancellationToken ct)
    {
        _lastAccessTimes[sessionId] = DateTime.UtcNow;
        if (!_sessions.TryGetValue(sessionId, out var process) || process.HasExited)
        {
            return new CliCommandExecutionResult(
                "Shell process not available.",
                -1,
                CliCommandExecutionState.ProcessUnavailable);
        }

        if (_runtimeStates.TryGetValue(sessionId, out var runtimeState))
        {
            runtimeState.State = CliSessionExecutionState.Running;
            runtimeState.LastActivityAt = DateTime.UtcNow;
            runtimeState.WaitingForInput = false;
            runtimeState.WaitingForInputSince = null;
            runtimeState.TerminationReason = CliSessionTerminationReason.None;
        }

        await PersistRuntimeSessionAsync(sessionId, command: command);

        var sentinel = CliCommandCompletionProtocol.CreateSentinel();
        var commandToRun = CliCommandCompletionProtocol.BuildCommand(
            command,
            sentinel,
            OperatingSystem.IsWindows());

        var initialStrippedLength = _strippedBuffers.TryGetValue(sessionId, out var strippedBuffer)
            ? strippedBuffer.Length
            : 0;

        await process.StandardInput.WriteLineAsync(commandToRun.AsMemory(), ct);
        await process.StandardInput.FlushAsync(ct);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        try
        {
            while (!cts.IsCancellationRequested && !process.HasExited)
            {
                var currentStripped = GetStrippedOutput(sessionId, initialStrippedLength);
                if (CliCommandCompletionProtocol.TryParseCompletion(
                    currentStripped,
                    sentinel,
                    out var completion))
                {
                    if (_runtimeStates.TryGetValue(sessionId, out var completedState))
                    {
                        completedState.LastActivityAt = DateTime.UtcNow;
                        completedState.WaitingForInput = false;
                        completedState.WaitingForInputSince = null;
                        completedState.State = completion.ExitCode == 0
                            ? CliSessionExecutionState.Completed
                            : CliSessionExecutionState.Failed;
                        completedState.TerminationReason = completion.ExitCode == 0
                            ? CliSessionTerminationReason.Completed
                            : CliSessionTerminationReason.ProcessExited;
                    }

                    await PersistRuntimeSessionAsync(sessionId, exitCode: completion.ExitCode, command: command);

                    return new CliCommandExecutionResult(
                        completion.CleanOutput,
                        completion.ExitCode,
                        completion.ExitCode == 0
                            ? CliCommandExecutionState.Completed
                            : CliCommandExecutionState.Failed);
                }

                await Task.Delay(50, ct);
            }

            if (cts.IsCancellationRequested)
            {
                if (_runtimeStates.TryGetValue(sessionId, out var runningState))
                {
                    runningState.LastActivityAt = DateTime.UtcNow;
                    runningState.State = CliSessionExecutionState.Running;
                    runningState.TerminationReason = CliSessionTerminationReason.None;
                    _ = PersistRuntimeSessionAsync(sessionId, command: command);
                }

                return new CliCommandExecutionResult(
                    GetStrippedOutput(sessionId, initialStrippedLength) + "\n\n[StillRunning] 本次等待预算已耗尽，终端命令仍在后台运行。可继续查看终端输出或等待会话结束。",
                    0,
                    CliCommandExecutionState.StillRunning);
            }
        }
        catch (OperationCanceledException)
        {
            MarkTermination(sessionId, CliSessionExecutionState.Cancelled, CliSessionTerminationReason.Cancelled);
            TerminateSession(sessionId);
            return new CliCommandExecutionResult(
                GetStrippedOutput(sessionId, initialStrippedLength) + "\n\n[Aborted] 执行被取消",
                -1,
                CliCommandExecutionState.Cancelled);
        }

        return new CliCommandExecutionResult(
            GetStrippedOutput(sessionId, initialStrippedLength),
            -1,
            CliCommandExecutionState.ProcessUnavailable);
    }

    /// <inheritdoc />
    public CliSessionTerminationReason GetTerminationReason(string sessionId)
    {
        return _runtimeStates.TryGetValue(sessionId, out var runtimeState)
            ? runtimeState.TerminationReason
            : CliSessionTerminationReason.None;
    }

    /// <inheritdoc />
    public string GetStrippedOutput(string sessionId, int startIndex = 0)
    {
        if (_strippedBuffers.TryGetValue(sessionId, out var sb))
        {
            lock (sb)
            {
                return startIndex < sb.Length ? sb.ToString(startIndex, sb.Length - startIndex) : string.Empty;
            }
        }

        return string.Empty;
    }

    /// <inheritdoc />
    public string GetRawOutput(string sessionId, int startIndex = 0)
    {
        if (_rawBuffers.TryGetValue(sessionId, out var sb))
        {
            lock (sb)
            {
                return startIndex < sb.Length ? sb.ToString(startIndex, sb.Length - startIndex) : string.Empty;
            }
        }

        return string.Empty;
    }

    /// <inheritdoc />
    public string TruncateOutput(string output, int headLimit = 1500, int tailLimit = 3500)
    {
        if (string.IsNullOrEmpty(output) || output.Length <= headLimit + tailLimit)
        {
            return output;
        }

        var head = output[..headLimit];
        var tail = output[^tailLimit..];
        return $"{head}\n\n... [Output Truncated. Total Length: {output.Length}, Showing Head {headLimit} & Tail {tailLimit} chars] ...\n\n{tail}";
    }

    /// <inheritdoc />
    public string GetOutputTail(string sessionId, int maxChars = 4000)
    {
        var output = GetStrippedOutput(sessionId);
        if (string.IsNullOrEmpty(output) || output.Length <= maxChars)
        {
            return output;
        }

        return output[^maxChars..];
    }

    /// <inheritdoc />
    public void TerminateSession(string sessionId)
    {
        var leaseSessionKey = _runtimeStates.TryGetValue(sessionId, out var runtimeState)
            ? runtimeState.LeaseSessionKey
            : sessionId;
        _sandboxSessionProvider.Release(leaseSessionKey);

        if (_sessions.TryRemove(sessionId, out var process))
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit();
                }
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }

            _logger.LogInformation("会话 {Id} 的 Shell 进程（包含整棵进程树）已强制终止", sessionId);
        }
    }

    /// <inheritdoc />
    public void CleanupBuffers(string sessionId)
    {
        _rawBuffers.TryRemove(sessionId, out _);
        _strippedBuffers.TryRemove(sessionId, out _);
        _lastAccessTimes.TryRemove(sessionId, out _);
        _runtimeStates.TryRemove(sessionId, out _);
    }

    /// <inheritdoc />
    public CliRuntimeCleanupResult CleanupExpiredSessions(
        TimeSpan idleTimeout,
        TimeSpan waitingForInputTimeout,
        TimeSpan maxRuntime)
    {
        var idleCount = 0;
        var waitingCount = 0;
        var maxRuntimeCount = 0;
        var now = DateTime.UtcNow;

        foreach (var runtimeState in _runtimeStates.Values.ToList())
        {
            if (!_sessions.TryGetValue(runtimeState.SessionKey, out var process) || process.HasExited)
            {
                continue;
            }

            if (runtimeState.WaitingForInput && runtimeState.WaitingForInputSince.HasValue &&
                now - runtimeState.WaitingForInputSince.Value > waitingForInputTimeout)
            {
                MarkTermination(
                    runtimeState.SessionKey,
                    CliSessionExecutionState.Reaped,
                    CliSessionTerminationReason.WaitingForInputTimeout);
                TerminateSession(runtimeState.SessionKey);
                CleanupBuffers(runtimeState.SessionKey);
                waitingCount++;
                continue;
            }

            if (now - runtimeState.StartedAt > maxRuntime)
            {
                MarkTermination(
                    runtimeState.SessionKey,
                    CliSessionExecutionState.TimedOut,
                    CliSessionTerminationReason.MaxRuntimeExceeded);
                TerminateSession(runtimeState.SessionKey);
                CleanupBuffers(runtimeState.SessionKey);
                maxRuntimeCount++;
                continue;
            }

            if (now - runtimeState.LastActivityAt > idleTimeout)
            {
                MarkTermination(
                    runtimeState.SessionKey,
                    CliSessionExecutionState.Reaped,
                    CliSessionTerminationReason.IdleTimeout);
                TerminateSession(runtimeState.SessionKey);
                CleanupBuffers(runtimeState.SessionKey);
                idleCount++;
            }
        }

        CleanupExpiredWarmShells();
        var activeLeaseSessionKeys = _runtimeStates.Values
            .Select(state => state.LeaseSessionKey)
            .Concat(_warmShells.Values.Select(entry => entry.Lease.SessionId))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        _sandboxSessionProvider.CleanupOrphanedLeases(activeLeaseSessionKeys);

        return new CliRuntimeCleanupResult(idleCount, waitingCount, maxRuntimeCount);
    }

    /// <inheritdoc />
    public void MarkSessionTerminated(
        string sessionId,
        CliSessionExecutionState state,
        CliSessionTerminationReason terminationReason)
    {
        MarkTermination(sessionId, state, terminationReason);
    }

    /// <inheritdoc />
    public CliSessionRuntimeSnapshot? GetRuntimeSnapshot(string sessionId)
    {
        if (!_runtimeStates.TryGetValue(sessionId, out var runtimeState))
        {
            return null;
        }

        return new CliSessionRuntimeSnapshot(
            runtimeState.SessionKey,
            runtimeState.WorkingDirectory,
            runtimeState.LockKey,
            runtimeState.StartedAt,
            runtimeState.LastActivityAt,
            runtimeState.WaitingForInput,
            runtimeState.WaitingForInputSince,
            runtimeState.State,
            runtimeState.TerminationReason);
    }

    /// <inheritdoc />
    public async Task<CliSessionRuntimeSnapshot?> WaitForExitAsync(
        string sessionId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            var snapshot = GetRuntimeSnapshot(sessionId);
            if (snapshot == null)
            {
                return null;
            }

            if (snapshot.State is CliSessionExecutionState.Completed
                or CliSessionExecutionState.Failed
                or CliSessionExecutionState.Cancelled
                or CliSessionExecutionState.TimedOut
                or CliSessionExecutionState.Reaped)
            {
                return snapshot;
            }

            await Task.Delay(250, cancellationToken);
        }

        return GetRuntimeSnapshot(sessionId);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var id in _sessions.Keys)
        {
            TerminateSession(id);
        }

        foreach (var key in _warmShells.Keys.ToList())
        {
            ReleaseWarmShell(key);
        }

        _sandboxSessionProvider.CleanupOrphanedLeases(Array.Empty<string>());
    }

    private Process StartPersistentShell(CliSandboxSessionLease lease, string sessionId)
    {
        var shellKind = lease.StartInfo.Environment.TryGetValue("DEVNEXUS_SHELL_KIND", out var configuredShellKind)
            ? configuredShellKind
            : string.Empty;
        var process = new Process { StartInfo = lease.StartInfo };
        var capturedSessionId = sessionId;
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                AppendToBuffers(capturedSessionId, e.Data + Environment.NewLine);
            }
        };

        process.Start();
        process.BeginErrorReadLine();

        if (string.Equals(shellKind, "powershell", StringComparison.OrdinalIgnoreCase))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await process.StandardInput.WriteLineAsync("[Console]::OutputEncoding = [System.Text.Encoding]::UTF8");
                    await process.StandardInput.FlushAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "设置 PowerShell 编码失败 (会话: {SessionId})", sessionId);
                }
            });
        }

        return process;
    }

    private async Task ReadLoop(string sessionId, Process process)
    {
        try
        {
            var buffer = new char[4096];
            int charsRead;
            while ((charsRead = await process.StandardOutput.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                var text = new string(buffer, 0, charsRead);
                AppendToBuffers(sessionId, text);
            }
        }
        catch (Exception ex)
        {
            MarkTermination(sessionId, CliSessionExecutionState.Failed, CliSessionTerminationReason.Error);
            _logger.LogDebug("会话 {Id} 读取循环异常关闭: {Msg}", sessionId, ex.Message);
        }
        finally
        {
            TerminateSession(sessionId);
        }
    }

    private void AppendToBuffers(string sessionId, string text)
    {
        if (text == null)
        {
            return;
        }

        var cleanText = CliOutputTextSanitizer.StripAnsi(text);

        if (_rawBuffers.TryGetValue(sessionId, out var raw))
        {
            lock (raw)
            {
                raw.Append(cleanText);
                MaintainWatermark(raw);
            }
        }

        if (_strippedBuffers.TryGetValue(sessionId, out var stripped))
        {
            lock (stripped)
            {
                stripped.Append(cleanText);
                MaintainWatermark(stripped);
                UpdateRuntimeState(sessionId, cleanText);
            }
        }

        OnOutputReceived?.Invoke(sessionId, cleanText);
    }

    private void MaintainWatermark(StringBuilder sb)
    {
        if (sb.Length > MaxBufferSize)
        {
            sb.Remove(0, sb.Length - MaxBufferSize / 2);
            sb.Insert(0, "... (由于内存安全策略已截断历史输出)\n");
        }
    }

    private void UpdateRuntimeState(string sessionId, string text)
    {
        if (!_runtimeStates.TryGetValue(sessionId, out var runtimeState))
        {
            return;
        }

        runtimeState.LastActivityAt = DateTime.UtcNow;
        _lastAccessTimes[sessionId] = runtimeState.LastActivityAt;

        if (CliOutputTextSanitizer.IsWaitingForInput(text))
        {
            runtimeState.WaitingForInput = true;
            runtimeState.WaitingForInputSince ??= DateTime.UtcNow;
            runtimeState.State = CliSessionExecutionState.WaitingForInput;
            _ = PersistRuntimeSessionAsync(sessionId);
        }
    }

    private void MarkTermination(
        string sessionId,
        CliSessionExecutionState state,
        CliSessionTerminationReason terminationReason)
    {
        if (_runtimeStates.TryGetValue(sessionId, out var runtimeState))
        {
            runtimeState.LastActivityAt = DateTime.UtcNow;
            runtimeState.WaitingForInput = false;
            runtimeState.WaitingForInputSince = null;
            runtimeState.State = state;
            runtimeState.TerminationReason = terminationReason;
            _ = PersistRuntimeSessionAsync(sessionId);
        }
    }

    private async Task PersistRuntimeSessionAsync(string sessionId, int? exitCode = null, string? command = null)
    {
        try
        {
            if (!_runtimeStates.TryGetValue(sessionId, out var runtimeState))
            {
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ICliExecSessionRepository>();
            var (userId, chatSessionId) = CliSessionPersistenceMapper.ParseSessionKey(sessionId);

            await repository.UpsertAsync(new CliExecSession
            {
                SessionKey = sessionId,
                UserId = userId,
                ChatSessionId = chatSessionId,
                ExecStatus = CliSessionPersistenceMapper.ToExecStatus(runtimeState.State),
                SessionMode = CliSessionMode.InteractiveShell,
                Command = command,
                WorkingDirectory = runtimeState.WorkingDirectory,
                RuntimeHost = "process-cli",
                StartedAt = runtimeState.StartedAt,
                LastActivityAt = runtimeState.LastActivityAt,
                WaitingForInput = runtimeState.WaitingForInput,
                WaitingForInputSince = runtimeState.WaitingForInputSince,
                ExitCode = exitCode,
                TerminationReason = runtimeState.TerminationReason.ToString(),
                IsActive = runtimeState.State is CliSessionExecutionState.Created
                    or CliSessionExecutionState.Running
                    or CliSessionExecutionState.WaitingForInput
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "持久化 CLI 执行会话失败 | SessionKey={SessionKey}", sessionId);
        }
    }

}
