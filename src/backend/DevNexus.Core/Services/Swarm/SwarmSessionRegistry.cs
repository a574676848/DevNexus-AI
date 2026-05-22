using System.Collections.Concurrent;
using System.Threading;

namespace DevNexus.Core.Services.Swarm;

/// <summary>
/// Swarm 会话控制状态枚举
/// </summary>
public enum SwarmControlStatus
{
    Running,
    Paused,
    Aborted
}

/// <summary>
/// Swarm 会话注册表 - Singleton 服务
/// 负责跨 Scoped 实例共享会话控制状态和 CancellationTokenSource
/// 解决会话编排链路与 SwarmHub 之间的跨作用域访问问题
/// </summary>
public class SwarmSessionRegistry
{
    private readonly ConcurrentDictionary<string, SwarmControlStatus> _sessionStates = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _sessionCts = new();
    private readonly ConcurrentDictionary<string, byte> _activePackageRetries = new();

    /// <summary>注册新会话并初始化 CancellationTokenSource</summary>
    public CancellationTokenSource RegisterSession(string sessionId)
    {
        var cts = new CancellationTokenSource();
        _sessionCts[sessionId] = cts;
        _sessionStates[sessionId] = SwarmControlStatus.Running;
        return cts;
    }

    /// <returns>当前会话状态，不存在返回 null</returns>
    public SwarmControlStatus? GetStatus(string sessionId)
        => _sessionStates.TryGetValue(sessionId, out var s) ? s : null;

    /// <summary>设置控制状态</summary>
    public void SetStatus(string sessionId, SwarmControlStatus status)
        => _sessionStates[sessionId] = status;

    /// <summary>暂停会话</summary>
    public void Pause(string sessionId) => SetStatus(sessionId, SwarmControlStatus.Paused);

    /// <summary>恢复会话</summary>
    public void Resume(string sessionId) => SetStatus(sessionId, SwarmControlStatus.Running);

    /// <summary>中止会话：同时取消 CancellationToken</summary>
    public void Abort(string sessionId)
    {
        SetStatus(sessionId, SwarmControlStatus.Aborted);
        ClearPackageRetries(sessionId);
        if (_sessionCts.TryGetValue(sessionId, out var cts))
        {
            cts.Cancel();
        }
    }

    /// <summary>尝试注册工作包重试占位，成功返回 true，表示当前调用拥有执行权。</summary>
    public bool TryBeginPackageRetry(string sessionId, string packageId)
        => _activePackageRetries.TryAdd(BuildPackageRetryKey(sessionId, packageId), 0);

    /// <summary>结束工作包重试占位。</summary>
    public void EndPackageRetry(string sessionId, string packageId)
        => _activePackageRetries.TryRemove(BuildPackageRetryKey(sessionId, packageId), out _);

    /// <summary>会话结束时清理资源</summary>
    public void UnregisterSession(string sessionId)
    {
        _sessionStates.TryRemove(sessionId, out _);
        ClearPackageRetries(sessionId);
        if (_sessionCts.TryRemove(sessionId, out var cts))
        {
            cts.Dispose();
        }
    }

    private static string BuildPackageRetryKey(string sessionId, string packageId)
        => $"{sessionId}::{packageId}";

    private void ClearPackageRetries(string sessionId)
    {
        var prefix = $"{sessionId}::";
        foreach (var key in _activePackageRetries.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                _activePackageRetries.TryRemove(key, out _);
            }
        }
    }
}
