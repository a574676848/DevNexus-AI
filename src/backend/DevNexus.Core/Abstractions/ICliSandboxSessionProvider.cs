using System.Diagnostics;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// CLI sandbox 会话租约。
/// </summary>
public sealed record CliSandboxSessionLease
{
    /// <summary>
    /// 会话标识。
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// 工作目录。
    /// </summary>
    public string WorkingDirectory { get; init; } = string.Empty;

    /// <summary>
    /// 工作目录锁键。
    /// </summary>
    public string LockKey { get; init; } = string.Empty;

    /// <summary>
    /// 进程启动信息。
    /// </summary>
    public ProcessStartInfo StartInfo { get; init; } = new();
}

/// <summary>
/// CLI sandbox 会话提供器。
/// 负责会话租约分配、工作目录串行保护与 shell 启动信息构建。
/// </summary>
public interface ICliSandboxSessionProvider
{
    /// <summary>
    /// 获取指定会话的 sandbox 租约。
    /// </summary>
    Task<CliSandboxSessionLease> AcquireAsync(
        string sessionId,
        string workingDirectory,
        CancellationToken cancellationToken);

    /// <summary>
    /// 释放指定会话的 sandbox 租约。
    /// </summary>
    void Release(string sessionId);

    /// <summary>
    /// 清理不再属于活动会话的孤儿租约。
    /// </summary>
    void CleanupOrphanedLeases(IReadOnlyCollection<string> activeSessionIds);
}
