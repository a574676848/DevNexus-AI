namespace DevNexus.Shared.Enums;

/// <summary>
/// CLI 执行快照状态。
/// </summary>
public enum CliExecCheckpointStatus
{
    /// <summary>
    /// 已创建。
    /// </summary>
    Created = 0,

    /// <summary>
    /// 已回滚。
    /// </summary>
    RolledBack = 1,

    /// <summary>
    /// 已失效。
    /// </summary>
    Invalidated = 2
}
