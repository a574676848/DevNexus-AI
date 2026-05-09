using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using DevNexus.Core.Abstractions;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevNexus.Infrastructure.Services.CliTerminal;

/// <summary>
/// 容器隔离 CLI sandbox 会话提供器。
/// 负责基于容器运行时构建交互式 shell 租约。
/// </summary>
public sealed class ContainerSandboxSessionProvider : ICliSandboxSessionProvider
{
    private readonly CliEnvironmentService _cliEnvironmentService;
    private readonly CliDockerContextResolver _dockerContextResolver;
    private readonly CliSandboxOptions _options;
    private readonly ILogger<ContainerSandboxSessionProvider> _logger;
    private readonly ConcurrentDictionary<string, string> _workingDirectoryLocks = new();
    private readonly ConcurrentDictionary<string, string> _sessionLockKeys = new();
    private readonly ConcurrentDictionary<string, ProcessStartInfo> _warmStartInfos = new();

    /// <summary>
    /// 构造函数。
    /// </summary>
    public ContainerSandboxSessionProvider(
        CliEnvironmentService cliEnvironmentService,
        CliDockerContextResolver dockerContextResolver,
        IOptions<CliSandboxOptions> options,
        ILogger<ContainerSandboxSessionProvider> logger)
    {
        _cliEnvironmentService = cliEnvironmentService;
        _dockerContextResolver = dockerContextResolver;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CliSandboxSessionLease> AcquireAsync(
        string sessionId,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedWorkingDirectory = Path.GetFullPath(workingDirectory);
        var warmed = _warmStartInfos.TryGetValue(normalizedWorkingDirectory, out var warmedStartInfo)
            ? CloneStartInfo(warmedStartInfo, normalizedWorkingDirectory)
            : null;
        var containerEnginePath = warmed?.FileName ?? ResolveContainerEnginePath();
        var selectedContextName = warmed == null
            ? await _dockerContextResolver.ResolveAsync(cancellationToken)
            : GetEnvironmentValue(warmed, "DOCKER_CONTEXT");

        var lockKey = BuildLockKey(normalizedWorkingDirectory);
        if (_workingDirectoryLocks.TryGetValue(lockKey, out var ownerSessionId) && ownerSessionId != sessionId)
        {
            throw new InvalidOperationException($"工作目录正在被其他 CLI 会话占用: {normalizedWorkingDirectory}");
        }

        _workingDirectoryLocks[lockKey] = sessionId;
        _sessionLockKeys[sessionId] = lockKey;

        var startInfo = warmed ?? BuildStartInfo(containerEnginePath, normalizedWorkingDirectory, selectedContextName);
        _logger.LogDebug(
            "[ContainerSandbox] 已分配会话租约 | SessionId={SessionId} WorkingDirectory={WorkingDirectory} Engine={Engine} Context={Context} Image={Image}",
            sessionId,
            normalizedWorkingDirectory,
            containerEnginePath,
            selectedContextName ?? "<current-default>",
            _options.ContainerImage);

        return new CliSandboxSessionLease
        {
            SessionId = sessionId,
            WorkingDirectory = normalizedWorkingDirectory,
            LockKey = lockKey,
            StartInfo = startInfo
        };
    }

    /// <inheritdoc />
    public void Release(string sessionId)
    {
        if (_sessionLockKeys.TryRemove(sessionId, out var lockKey))
        {
            _workingDirectoryLocks.TryRemove(lockKey, out _);
        }
    }

    /// <inheritdoc />
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
                "[ContainerSandbox] 已清理孤儿租约 | SessionId={SessionId}",
                sessionId);
        }
    }

    /// <summary>
    /// 预热指定工作目录对应的容器启动模板。
    /// </summary>
    public async Task WarmAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedWorkingDirectory = Path.GetFullPath(workingDirectory);
        var containerEnginePath = ResolveContainerEnginePath();
        var selectedContextName = await _dockerContextResolver.ResolveAsync(cancellationToken);
        _warmStartInfos[normalizedWorkingDirectory] = BuildStartInfo(
            containerEnginePath,
            normalizedWorkingDirectory,
            selectedContextName);

        _logger.LogDebug(
            "[ContainerSandbox] 已预热工作目录模板 | WorkingDirectory={WorkingDirectory} Context={Context}",
            normalizedWorkingDirectory,
            selectedContextName ?? "<current-default>");
    }

    private string ResolveContainerEnginePath()
    {
        if (_cliEnvironmentService.IsCommandAvailable(_options.ContainerEngineCommand, out var containerEnginePath))
        {
            return containerEnginePath;
        }

        throw new InvalidOperationException($"未找到容器运行时命令：{_options.ContainerEngineCommand}");
    }

    private ProcessStartInfo BuildStartInfo(string containerEnginePath, string workingDirectory, string? selectedContextName)
    {
        var mountPath = NormalizeContainerPath(_options.ContainerWorkingDirectory);
        var shell = string.IsNullOrWhiteSpace(_options.ContainerShell)
            ? "/bin/bash"
            : _options.ContainerShell.Trim();

        var arguments = new StringBuilder("run --rm -i");
        if (_options.DisableNetwork)
        {
            arguments.Append(" --network none");
        }

        if (_options.MemoryLimitMb > 0)
        {
            arguments.Append(CultureInfo.InvariantCulture, $" --memory {_options.MemoryLimitMb}m");
        }

        if (_options.CpuLimit > 0)
        {
            arguments.Append(CultureInfo.InvariantCulture, $" --cpus {_options.CpuLimit:0.##}");
        }

        arguments.Append(" -e DEVNEXUS_SANDBOX=1");
        arguments.Append(" -e LANG=en_US.UTF-8 -e LC_ALL=en_US.UTF-8");
        arguments.Append(CultureInfo.InvariantCulture, $" -v {QuoteArgument($"{workingDirectory}:{mountPath}")}");
        arguments.Append(CultureInfo.InvariantCulture, $" -w {QuoteArgument(mountPath)}");
        arguments.Append(CultureInfo.InvariantCulture, $" {QuoteArgument(_options.ContainerImage)}");
        arguments.Append(CultureInfo.InvariantCulture, $" {QuoteArgument(shell)}");

        var startInfo = new ProcessStartInfo
        {
            FileName = containerEnginePath,
            Arguments = arguments.ToString(),
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        startInfo.Environment.Remove("DOCKER_HOST");
        if (!string.IsNullOrWhiteSpace(selectedContextName))
        {
            startInfo.Environment["DOCKER_CONTEXT"] = selectedContextName;
        }
        else
        {
            startInfo.Environment.Remove("DOCKER_CONTEXT");
        }

        startInfo.Environment["DEVNEXUS_SHELL_KIND"] = "posix";
        startInfo.Environment["DEVNEXUS_SANDBOX_MODE"] = CliSandboxMode.ContainerIsolated.ToString();
        return startInfo;
    }

    private static string BuildLockKey(string workingDirectory)
    {
        return Path.GetFullPath(workingDirectory)
            .TrimEnd(Path.DirectorySeparatorChar)
            .ToLowerInvariant();
    }

    private static string NormalizeContainerPath(string path)
    {
        var normalized = path.Replace('\\', '/').Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "/workspace";
        }

        return normalized.StartsWith('/') ? normalized : $"/{normalized}";
    }

    private static string QuoteArgument(string value)
    {
        return value.Contains(' ') ? $"\"{value.Replace("\"", "\\\"")}\"" : value;
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

    private static string? GetEnvironmentValue(ProcessStartInfo startInfo, string key)
    {
        return startInfo.Environment.TryGetValue(key, out var value)
            ? value
            : null;
    }
}
