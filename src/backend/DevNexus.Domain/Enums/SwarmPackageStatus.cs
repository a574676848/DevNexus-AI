namespace DevNexus.Domain.Enums;

/// <summary>
/// 上下文工作包状态。
/// </summary>
public enum SwarmPackageStatus
{
    /// <summary>
    /// 待规划。
    /// </summary>
    Pending = 0,

    /// <summary>
    /// 已准备执行。
    /// </summary>
    Ready = 1,

    /// <summary>
    /// 执行中。
    /// </summary>
    InProgress = 2,

    /// <summary>
    /// 已完成。
    /// </summary>
    Completed = 3,

    /// <summary>
    /// 已失败。
    /// </summary>
    Failed = 4,

    /// <summary>
    /// 评估中。
    /// </summary>
    Evaluating = 5,

    /// <summary>
    /// 已中止。
    /// </summary>
    Aborted = 6
}
