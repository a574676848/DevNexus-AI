namespace DevNexus.Core.Models.Cli;

/// <summary>
/// CLI 运行时快照，用于前后端状态恢复与宿主层状态查询。
/// </summary>
public sealed record CliSessionRuntimeSnapshot(
    string SessionKey,
    string WorkingDirectory,
    string LockKey,
    DateTime StartedAt,
    DateTime LastActivityAt,
    bool WaitingForInput,
    DateTime? WaitingForInputSince,
    CliSessionExecutionState State,
    CliSessionTerminationReason TerminationReason);