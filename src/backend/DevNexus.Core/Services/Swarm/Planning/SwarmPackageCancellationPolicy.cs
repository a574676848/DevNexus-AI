using DevNexus.Domain.Enums;
using DevNexus.Domain.Models.Swarm;

namespace DevNexus.Core.Services.Swarm.Planning;

/// <summary>
/// Swarm 工作包取消策略。
/// </summary>
internal static class SwarmPackageCancellationPolicy
{
    private const string CancelledReason = "Swarm 工作包调度已取消。";

    /// <summary>
    /// 标记尚未启动的工作包为已中止。
    /// </summary>
    public static bool MarkPendingPackagesAborted(
        IEnumerable<ContextWorkPackage> packages,
        CancellationToken cancellationToken)
    {
        if (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        var changed = false;
        foreach (var package in packages)
        {
            if (package.Status is SwarmPackageStatus.Completed or SwarmPackageStatus.Failed or SwarmPackageStatus.Aborted)
            {
                continue;
            }

            package.Status = SwarmPackageStatus.Aborted;
            package.FailureReason ??= CancelledReason;
            package.Result ??= CancelledReason;
            package.UpdatedAt = DateTime.UtcNow;
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// 标记当前执行包为已中止，并保留取消事实。
    /// </summary>
    public static void MarkCurrentPackageAborted(ContextWorkPackage package)
    {
        package.Status = SwarmPackageStatus.Aborted;
        package.FailureReason ??= CancelledReason;
        package.Result ??= CancelledReason;
        package.UpdatedAt = DateTime.UtcNow;
    }
}
