using System.Collections.Concurrent;
using DevNexus.Core.Abstractions;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevNexus.Infrastructure.Services.CliTerminal;

/// <summary>
/// 可配置 CLI sandbox 会话提供器。
/// 根据配置在本地受限模式和容器隔离模式之间切换。
/// </summary>
public sealed class ConfigurableCliSandboxSessionProvider : ICliSandboxSessionProvider, ICliSandboxWarmPool
{
    private readonly LocalRestrictedSandboxSessionProvider _localRestrictedProvider;
    private readonly ContainerSandboxSessionProvider _containerProvider;
    private readonly IOptionsMonitor<CliSandboxOptions> _optionsMonitor;
    private readonly ILogger<ConfigurableCliSandboxSessionProvider> _logger;
    private readonly ConcurrentDictionary<string, CliSandboxMode> _sessionModes = new();

    /// <summary>
    /// 构造函数。
    /// </summary>
    public ConfigurableCliSandboxSessionProvider(
        LocalRestrictedSandboxSessionProvider localRestrictedProvider,
        ContainerSandboxSessionProvider containerProvider,
        IOptionsMonitor<CliSandboxOptions> optionsMonitor,
        ILogger<ConfigurableCliSandboxSessionProvider> logger)
    {
        _localRestrictedProvider = localRestrictedProvider;
        _containerProvider = containerProvider;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CliSandboxSessionLease> AcquireAsync(
        string sessionId,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var mode = _optionsMonitor.CurrentValue.Mode;
        var provider = ResolveProvider(mode);
        var lease = await provider.AcquireAsync(sessionId, workingDirectory, cancellationToken);
        _sessionModes[sessionId] = mode;

        _logger.LogDebug(
            "[CliSandbox] 已选择 sandbox provider | SessionId={SessionId} Mode={Mode}",
            sessionId,
            mode);

        return lease;
    }

    /// <inheritdoc />
    public void Release(string sessionId)
    {
        var provider = ResolveProvider(_sessionModes.TryRemove(sessionId, out var mode)
            ? mode
            : _optionsMonitor.CurrentValue.Mode);
        provider.Release(sessionId);
    }

    /// <inheritdoc />
    public void CleanupOrphanedLeases(IReadOnlyCollection<string> activeSessionIds)
    {
        _localRestrictedProvider.CleanupOrphanedLeases(activeSessionIds);
        _containerProvider.CleanupOrphanedLeases(activeSessionIds);

        if (_sessionModes.Count == 0)
        {
            return;
        }

        var activeSet = activeSessionIds.Count == 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(activeSessionIds, StringComparer.Ordinal);

        foreach (var sessionId in _sessionModes.Keys.ToList())
        {
            if (!activeSet.Contains(sessionId))
            {
                _sessionModes.TryRemove(sessionId, out _);
            }
        }
    }

    /// <inheritdoc />
    public Task WarmAsync(string workingDirectory, CancellationToken cancellationToken = default)
    {
        var mode = _optionsMonitor.CurrentValue.Mode;
        return mode switch
        {
            CliSandboxMode.ContainerIsolated => _containerProvider.WarmAsync(workingDirectory, cancellationToken),
            _ => _localRestrictedProvider.WarmAsync(workingDirectory, cancellationToken)
        };
    }

    private ICliSandboxSessionProvider ResolveProvider(CliSandboxMode mode)
    {
        return mode switch
        {
            CliSandboxMode.ContainerIsolated => _containerProvider,
            _ => _localRestrictedProvider
        };
    }
}
