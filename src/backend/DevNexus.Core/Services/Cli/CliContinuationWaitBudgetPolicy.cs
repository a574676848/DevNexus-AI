namespace DevNexus.Core.Services.Cli;

/// <summary>
/// CLI 模型可见续接等待预算策略。
/// </summary>
public static class CliContinuationWaitBudgetPolicy
{
    /// <summary>
    /// 最小等待毫秒数。
    /// </summary>
    public const int MinimumWaitMilliseconds = 1000;

    /// <summary>
    /// 默认等待毫秒数。
    /// </summary>
    public const int DefaultWaitMilliseconds = 10000;

    /// <summary>
    /// 最大等待毫秒数。
    /// </summary>
    public const int MaximumWaitMilliseconds = 30000;

    /// <summary>
    /// 将模型传入的等待预算收敛到受控区间。
    /// </summary>
    public static TimeSpan Normalize(int timeoutMilliseconds)
    {
        return TimeSpan.FromMilliseconds(
            Math.Clamp(timeoutMilliseconds, MinimumWaitMilliseconds, MaximumWaitMilliseconds));
    }
}
