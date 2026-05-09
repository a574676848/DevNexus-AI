using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using DevNexus.Core.Abstractions;
using DevNexus.Core.Models.Cli;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.CliTerminal;

/// <summary>
/// 基于标准 Process 的 CLI 运行时宿主。
/// </summary>
public sealed class ProcessCliRuntimeHost : ICliProcessRegistry
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
    private static readonly Regex AnsiRegex = new(@"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])", RegexOptions.Compiled);
    private static readonly Regex[] WaitingInputPatterns =
    [
        new Regex(@"(?i)password\s*[:：]?$", RegexOptions.Compiled),
        new Regex(@"(?i)continue\?\s*\[y/n\]", RegexOptions.Compiled),
        new Regex(@"(?i)press\s+enter\s+to\s+continue", RegexOptions.Compiled),
        new Regex(@"(?i)confirm\s*\[y/n\]", RegexOptions.Compiled),
        new Regex(@"(?i)enter\s+.*[:：]$", RegexOptions.Compiled)
    ];

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
    public async Task<(string Output, int ExitCode)> ExecuteAndWaitAsync(
        string sessionId,
        string command,
        TimeSpan timeout,
        CancellationToken ct)
    {
        _lastAccessTimes[sessionId] = DateTime.UtcNow;
        if (!_sessions.TryGetValue(sessionId, out var process) || process.HasExited)
        {
            return ("Shell process not available.", -1);
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

        var isWindows = OperatingSystem.IsWindows();
        var sentinel = $"__EXIT_{Guid.NewGuid():N}__";

        var commandToRun = isWindows
            ? $"{command}; echo '{sentinel}'; echo $LASTEXITCODE"
            : $"{command}; echo '{sentinel}'; echo $?";

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
                if (currentStripped.Contains(sentinel, StringComparison.Ordinal))
                {
                    var lines = currentStripped.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
                    var exitCodeStr = lines.LastOrDefault();
                    int.TryParse(exitCodeStr, out var exitCode);

                    var cleanOutput = currentStripped[..currentStripped.IndexOf(sentinel, StringComparison.Ordinal)].TrimEnd();
                    if (_runtimeStates.TryGetValue(sessionId, out var completedState))
                    {
                        completedState.LastActivityAt = DateTime.UtcNow;
                        completedState.WaitingForInput = false;
                        completedState.WaitingForInputSince = null;
                        completedState.State = exitCode == 0
                            ? CliSessionExecutionState.Completed
                            : CliSessionExecutionState.Failed;
                        completedState.TerminationReason = exitCode == 0
                            ? CliSessionTerminationReason.Completed
                            : CliSessionTerminationReason.ProcessExited;
                    }

                    await PersistRuntimeSessionAsync(sessionId, exitCode: exitCode, command: command);

                    return (cleanOutput, exitCode);
                }

                await Task.Delay(50, ct);
            }

            if (cts.IsCancellationRequested)
            {
                MarkTermination(sessionId, CliSessionExecutionState.TimedOut, CliSessionTerminationReason.MaxRuntimeExceeded);
                TerminateSession(sessionId);
                return (GetStrippedOutput(sessionId, initialStrippedLength) + "\n\n[Timeout] 进程执行超时被强制斩杀", 124);
            }
        }
        catch (OperationCanceledException)
        {
            MarkTermination(sessionId, CliSessionExecutionState.Cancelled, CliSessionTerminationReason.Cancelled);
            TerminateSession(sessionId);
            return (GetStrippedOutput(sessionId, initialStrippedLength) + "\n\n[Aborted] 执行被取消", -1);
        }

        return (GetStrippedOutput(sessionId, initialStrippedLength), -1);
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

    /// <summary>
    /// 预热指定工作目录的可消费 shell。
    /// </summary>
    public async Task WarmShellAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        CleanupExpiredWarmShells();

        var normalizedWorkingDirectory = Path.GetFullPath(workingDirectory);
        if (_warmShells.TryGetValue(normalizedWorkingDirectory, out var existing)
            && !existing.Process.HasExited)
        {
            return;
        }

        var warmSessionId = $"warm:{Guid.NewGuid():N}";
        var lease = await _sandboxSessionProvider.AcquireAsync(warmSessionId, normalizedWorkingDirectory, cancellationToken);
        var process = StartPersistentShell(lease, warmSessionId);

        if (process.HasExited)
        {
            _sandboxSessionProvider.Release(warmSessionId);
            process.Dispose();
            return;
        }

        var entry = new WarmShellEntry
        {
            WorkingDirectory = normalizedWorkingDirectory,
            Lease = lease,
            Process = process,
            WarmedAt = DateTime.UtcNow
        };

        if (_warmShells.TryGetValue(normalizedWorkingDirectory, out var previous))
        {
            CleanupWarmShell(previous);
        }

        _warmShells[normalizedWorkingDirectory] = entry;
        _logger.LogDebug(
            "[CliRuntimeWarmPool] 已预热 shell | WorkingDirectory={WorkingDirectory} WarmSession={WarmSession}",
            normalizedWorkingDirectory,
            warmSessionId);
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

        if (_rawBuffers.TryGetValue(sessionId, out var raw))
        {
            lock (raw)
            {
                raw.Append(text);
                MaintainWatermark(raw);
            }
        }

        if (_strippedBuffers.TryGetValue(sessionId, out var stripped))
        {
            lock (stripped)
            {
                var cleanText = AnsiRegex.Replace(text, string.Empty);
                stripped.Append(cleanText);
                MaintainWatermark(stripped);
                UpdateRuntimeState(sessionId, cleanText);
            }
        }

        OnOutputReceived?.Invoke(sessionId, text);
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

        if (WaitingInputPatterns.Any(pattern => pattern.IsMatch(text.Trim())))
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
            var (userId, chatSessionId) = ParseSessionKey(sessionId);

            await repository.UpsertAsync(new CliExecSession
            {
                SessionKey = sessionId,
                UserId = userId,
                ChatSessionId = chatSessionId,
                ExecStatus = ToExecStatus(runtimeState.State),
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

    private static CliExecStatus ToExecStatus(CliSessionExecutionState state)
    {
        return state switch
        {
            CliSessionExecutionState.Created => CliExecStatus.Requested,
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

    private static (Guid? UserId, Guid? ChatSessionId) ParseSessionKey(string sessionKey)
    {
        var parts = sessionKey.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return (null, null);
        }

        Guid? userId = null;
        Guid? chatSessionId = null;

        if (parts[0].Length == 32)
        {
            var hex = parts[0];
            if (Guid.TryParseExact(
                $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..32]}",
                "D",
                out var parsedUserId))
            {
                userId = parsedUserId;
            }
        }

        if (Guid.TryParse(parts[1], out var parsedChatSessionId))
        {
            chatSessionId = parsedChatSessionId;
        }

        return (userId, chatSessionId);
    }

    private WarmShellEntry? TryTakeWarmShell(string workingDirectory)
    {
        if (!_warmShells.TryRemove(workingDirectory, out var entry))
        {
            return null;
        }

        if (entry.Process.HasExited)
        {
            CleanupWarmShell(entry);
            return null;
        }

        return entry;
    }

    private void CleanupExpiredWarmShells()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in _warmShells.ToArray())
        {
            if (entry.Value.Process.HasExited || now - entry.Value.WarmedAt > WarmShellMaxAge)
            {
                if (_warmShells.TryRemove(entry.Key, out var removed))
                {
                    CleanupWarmShell(removed);
                }
            }
        }
    }

    private void ReleaseWarmShell(string workingDirectory)
    {
        if (_warmShells.TryRemove(workingDirectory, out var entry))
        {
            CleanupWarmShell(entry);
        }
    }

    private void CleanupWarmShell(WarmShellEntry entry)
    {
        try
        {
            _sandboxSessionProvider.Release(entry.Lease.SessionId);
            if (!entry.Process.HasExited)
            {
                entry.Process.Kill(entireProcessTree: true);
                entry.Process.WaitForExit();
            }
        }
        catch
        {
        }
        finally
        {
            entry.Process.Dispose();
        }
    }

    private sealed class CliSessionRuntimeState
    {
        public string SessionKey { get; set; } = string.Empty;

        public string WorkingDirectory { get; set; } = string.Empty;

        public string LockKey { get; set; } = string.Empty;

        public string LeaseSessionKey { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime StartedAt { get; set; }

        public DateTime LastActivityAt { get; set; }

        public bool WaitingForInput { get; set; }

        public DateTime? WaitingForInputSince { get; set; }

        public CliSessionExecutionState State { get; set; }

        public CliSessionTerminationReason TerminationReason { get; set; }
    }

    private sealed class WarmShellEntry
    {
        public string WorkingDirectory { get; init; } = string.Empty;

        public CliSandboxSessionLease Lease { get; init; } = new();

        public Process Process { get; init; } = null!;

        public DateTime WarmedAt { get; init; }
    }
}
