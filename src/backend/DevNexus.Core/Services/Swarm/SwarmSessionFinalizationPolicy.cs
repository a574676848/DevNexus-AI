using DevNexus.Domain.Enums;
using DevNexus.Domain.Entities;
using DevNexus.Domain.Models.Swarm;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Swarm;

/// <summary>
/// Swarm 会话收尾策略。
/// </summary>
public static class SwarmSessionFinalizationPolicy
{
    private const string CancelledReason = "Swarm 执行已取消。";
    private const string FailedFallbackReason = "Swarm 执行失败。";

    /// <summary>
    /// 构建取消收尾结果。
    /// </summary>
    public static SwarmSessionFinalizationResult BuildCancelled(
        IReadOnlyCollection<ContextWorkPackage> packages)
    {
        MarkUnfinishedPackages(packages, SwarmPackageStatus.Aborted, CancelledReason);
        return new SwarmSessionFinalizationResult(
            SwarmStatus.Aborted,
            CancelledReason,
            NotifyFailure: false,
            NotifyCancellation: true);
    }

    /// <summary>
    /// 构建失败收尾结果。
    /// </summary>
    public static SwarmSessionFinalizationResult BuildFailed(
        IReadOnlyCollection<ContextWorkPackage> packages,
        Exception exception)
    {
        var reason = string.IsNullOrWhiteSpace(exception.Message)
            ? FailedFallbackReason
            : exception.Message;
        MarkUnfinishedPackages(packages, SwarmPackageStatus.Failed, reason);

        return new SwarmSessionFinalizationResult(
            SwarmStatus.Failed,
            reason,
            NotifyFailure: true,
            NotifyCancellation: false);
    }

    /// <summary>
    /// 构建异常中断恢复收尾结果。
    /// </summary>
    public static SwarmSessionFinalizationResult BuildInterruptedRecovery(
        ICollection<ContextWorkPackageRecord> packageRecords,
        string reason)
    {
        var normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? FailedFallbackReason
            : reason;
        MarkUnfinishedPackageRecords(packageRecords, normalizedReason);

        return new SwarmSessionFinalizationResult(
            SwarmStatus.Failed,
            normalizedReason,
            NotifyFailure: true,
            NotifyCancellation: false);
    }

    /// <summary>
    /// 构建用户主动取消的持久化收尾结果。
    /// </summary>
    public static SwarmSessionFinalizationResult BuildUserAbort(
        ICollection<ContextWorkPackageRecord> packageRecords)
    {
        MarkCancelledPackageRecords(packageRecords, CancelledReason);

        return new SwarmSessionFinalizationResult(
            SwarmStatus.Aborted,
            CancelledReason,
            NotifyFailure: false,
            NotifyCancellation: true);
    }

    private static void MarkUnfinishedPackages(
        IReadOnlyCollection<ContextWorkPackage> packages,
        SwarmPackageStatus status,
        string reason)
    {
        foreach (var package in packages.Where(package => package.Status != SwarmPackageStatus.Completed))
        {
            package.Status = status;
            package.FailureReason ??= reason;
            package.Result ??= reason;
            package.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static void MarkUnfinishedPackageRecords(
        ICollection<ContextWorkPackageRecord> packageRecords,
        string reason)
    {
        foreach (var package in packageRecords.Where(package => package.Status != SwarmTaskStatus.Completed))
        {
            package.Status = SwarmTaskStatus.Failed;
            package.FailureReason ??= reason;
            package.Result ??= reason;
            package.CompletedAt ??= DateTime.UtcNow;
            package.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static void MarkCancelledPackageRecords(
        ICollection<ContextWorkPackageRecord> packageRecords,
        string reason)
    {
        foreach (var package in packageRecords.Where(package => package.Status != SwarmTaskStatus.Completed))
        {
            package.Status = SwarmTaskStatus.Skipped;
            package.FailureReason ??= reason;
            package.Result ??= reason;
            package.CompletedAt ??= DateTime.UtcNow;
            package.UpdatedAt = DateTime.UtcNow;
        }
    }
}
