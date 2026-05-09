namespace DevNexus.Shared.Constants;

/// <summary>
/// Swarm 生命周期事件的字符串协议定义。
/// </summary>
public static class SwarmEventNames
{
    /// <summary>
    /// 工作包快照已更新。
    /// </summary>
    public const string ContextPackagesUpdated = "context_packages_updated";

    /// <summary>
    /// Swarm 已启动。
    /// </summary>
    public const string Started = "started";

    /// <summary>
    /// Swarm 已完成。
    /// </summary>
    public const string Completed = "completed";

    /// <summary>
    /// Swarm 已失败或被取消。
    /// </summary>
    public const string Failed = "failed";

    /// <summary>
    /// 规范化 Swarm 事件值。
    /// </summary>
    public static string Normalize(string? value, string fallback = Started)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            Started => Started,
            Completed => Completed,
            Failed => Failed,
            _ => fallback
        };
    }

    /// <summary>
    /// 是否为启动事件。
    /// </summary>
    public static bool IsStarted(string? value)
    {
        return Normalize(value) == Started;
    }

    /// <summary>
    /// 是否为结束事件。
    /// </summary>
    public static bool IsTerminal(string? value)
    {
        var normalized = Normalize(value, string.Empty);
        return normalized is Completed or Failed;
    }
}
