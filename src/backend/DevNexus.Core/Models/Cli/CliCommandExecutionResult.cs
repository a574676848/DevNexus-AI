namespace DevNexus.Core.Models.Cli;

/// <summary>
/// CLI 命令执行等待结果。
/// </summary>
public sealed record CliCommandExecutionResult(
    string Output,
    int ExitCode,
    CliCommandExecutionState State)
{
    /// <summary>
    /// 命令是否已经进入终态。
    /// </summary>
    public bool IsTerminal => State is CliCommandExecutionState.Completed
        or CliCommandExecutionState.Failed
        or CliCommandExecutionState.Cancelled
        or CliCommandExecutionState.ProcessUnavailable;
}

/// <summary>
/// CLI 命令等待状态。
/// </summary>
public enum CliCommandExecutionState
{
    /// <summary>
    /// 命令已成功完成。
    /// </summary>
    Completed = 0,

    /// <summary>
    /// 命令已完成但退出码非零。
    /// </summary>
    Failed = 1,

    /// <summary>
    /// 本次等待预算已耗尽，但命令仍在运行。
    /// </summary>
    StillRunning = 2,

    /// <summary>
    /// 执行被取消。
    /// </summary>
    Cancelled = 3,

    /// <summary>
    /// 底层 shell 不可用。
    /// </summary>
    ProcessUnavailable = 4
}
