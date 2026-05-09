namespace DevNexus.Core.Models.Cli;

/// <summary>
/// CLI 运行时批量清理结果。
/// </summary>
public sealed record CliRuntimeCleanupResult(
    int IdleSessions,
    int WaitingSessions,
    int MaxRuntimeSessions);