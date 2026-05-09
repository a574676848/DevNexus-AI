using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using DevNexus.Core.Abstractions;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.CliTerminal;

/// <summary>
/// 本地受限 CLI sandbox 会话提供器。
/// 当前负责工作目录级串行保护与本地 shell 启动配置。
/// </summary>
public sealed class LocalRestrictedSandboxSessionProvider : ICliSandboxSessionProvider
{
    private readonly CliEnvironmentService _cliEnvironmentService;
    private readonly ILogger<LocalRestrictedSandboxSessionProvider> _logger;
    private readonly ConcurrentDictionary<string, string> _workingDirectoryLocks = new();
    private readonly ConcurrentDictionary<string, string> _sessionLockKeys = new();
    private readonly ConcurrentDictionary<string, ProcessStartInfo> _warmStartInfos = new();

    public LocalRestrictedSandboxSessionProvider(
        CliEnvironmentService cliEnvironmentService,
        ILogger<LocalRestrictedSandboxSessionProvider> logger)
    {
        _cliEnvironmentService = cliEnvironmentService;
        _logger = logger;
    }

    public Task<CliSandboxSessionLease> AcquireAsync(
        string sessionId,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedWorkingDirectory = Path.GetFullPath(workingDirectory);
        var lockKey = BuildLockKey(normalizedWorkingDirectory);

        if (_workingDirectoryLocks.TryGetValue(lockKey, out var ownerSessionId) && ownerSessionId != sessionId)
        {
            throw new InvalidOperationException($"工作目录正在被其他 CLI 会话占用: {normalizedWorkingDirectory}");
        }

        _workingDirectoryLocks[lockKey] = sessionId;
        _sessionLockKeys[sessionId] = lockKey;

        var startInfo = _warmStartInfos.TryGetValue(normalizedWorkingDirectory, out var warmedStartInfo)
            ? CloneStartInfo(warmedStartInfo, normalizedWorkingDirectory)
            : BuildStartInfo(normalizedWorkingDirectory);
        _logger.LogDebug(
            "[LocalRestrictedSandbox] 已分配会话租约 | SessionId={SessionId} WorkingDirectory={WorkingDirectory} Shell={Shell}",
            sessionId,
            normalizedWorkingDirectory,
            startInfo.FileName);

        return Task.FromResult(new CliSandboxSessionLease
        {
            SessionId = sessionId,
            WorkingDirectory = normalizedWorkingDirectory,
            LockKey = lockKey,
            StartInfo = startInfo
        });
    }

    public void Release(string sessionId)
    {
        if (_sessionLockKeys.TryRemove(sessionId, out var lockKey))
        {
            _workingDirectoryLocks.TryRemove(lockKey, out _);
        }
    }

    public void CleanupOrphanedLeases(IReadOnlyCollection<string> activeSessionIds)
    {
        if (_sessionLockKeys.Count == 0)
        {
            return;
        }

        var activeSet = activeSessionIds.Count == 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(activeSessionIds, StringComparer.Ordinal);

        foreach (var sessionId in _sessionLockKeys.Keys.ToList())
        {
            if (activeSet.Contains(sessionId))
            {
                continue;
            }

            Release(sessionId);
            _logger.LogDebug(
                "[LocalRestrictedSandbox] 已清理孤儿租约 | SessionId={SessionId}",
                sessionId);
        }
    }

    /// <summary>
    /// 预热指定工作目录对应的 shell 启动模板。
    /// </summary>
    public Task WarmAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedWorkingDirectory = Path.GetFullPath(workingDirectory);
        _warmStartInfos[normalizedWorkingDirectory] = BuildStartInfo(normalizedWorkingDirectory);

        _logger.LogDebug(
            "[LocalRestrictedSandbox] 已预热工作目录模板 | WorkingDirectory={WorkingDirectory}",
            normalizedWorkingDirectory);

        return Task.CompletedTask;
    }

    private ProcessStartInfo BuildStartInfo(string workingDirectory)
    {
        var shellPath = _cliEnvironmentService.GetDefaultShell();
        var shellArguments = _cliEnvironmentService.GetDefaultArguments();

        var startInfo = new ProcessStartInfo
        {
            FileName = shellPath,
            Arguments = shellArguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        startInfo.Environment["FORCE_COLOR"] = "1";
        startInfo.Environment["CLICOLOR_FORCE"] = "1";
        startInfo.Environment["TERM"] = "xterm-256color";
        startInfo.Environment["DEVNEXUS_SANDBOX"] = "1";
        startInfo.Environment["DEVNEXUS_SHELL_KIND"] = shellPath.EndsWith("cmd.exe", StringComparison.OrdinalIgnoreCase)
            ? "cmd"
            : shellPath.EndsWith("pwsh.exe", StringComparison.OrdinalIgnoreCase)
                || shellPath.EndsWith("powershell.exe", StringComparison.OrdinalIgnoreCase)
                ? "powershell"
                : "posix";
        startInfo.Environment["DEVNEXUS_SANDBOX_MODE"] = CliSandboxMode.LocalRestricted.ToString();
        startInfo.Environment["LANG"] = "en_US.UTF-8";
        startInfo.Environment["LC_ALL"] = "en_US.UTF-8";

        return startInfo;
    }

    private static ProcessStartInfo CloneStartInfo(ProcessStartInfo source, string workingDirectory)
    {
        var cloned = new ProcessStartInfo
        {
            FileName = source.FileName,
            Arguments = source.Arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = source.RedirectStandardInput,
            RedirectStandardOutput = source.RedirectStandardOutput,
            RedirectStandardError = source.RedirectStandardError,
            UseShellExecute = source.UseShellExecute,
            CreateNoWindow = source.CreateNoWindow,
            StandardOutputEncoding = source.StandardOutputEncoding,
            StandardErrorEncoding = source.StandardErrorEncoding
        };

        foreach (var entry in source.Environment)
        {
            cloned.Environment[entry.Key] = entry.Value;
        }

        return cloned;
    }

    private static string BuildLockKey(string workingDirectory)
    {
        return Path.GetFullPath(workingDirectory)
            .TrimEnd(Path.DirectorySeparatorChar)
            .ToLowerInvariant();
    }
}
