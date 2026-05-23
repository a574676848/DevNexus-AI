using DevNexus.Domain.Entities;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Swarm;

/// <summary>
/// Swarm 会话复盘原因。
/// </summary>
public static class SwarmSessionReviewReasons
{
    /// <summary>
    /// 会话不存在。
    /// </summary>
    public const string MissingSession = "missing-session";

    /// <summary>
    /// 仍有工作包未进入终态。
    /// </summary>
    public const string NonTerminalPackages = "non-terminal-packages";

    /// <summary>
    /// 失败工作包可重试。
    /// </summary>
    public const string FailedPackagesRetryable = "failed-packages-retryable";

    /// <summary>
    /// 会话结果可复盘。
    /// </summary>
    public const string ReviewableResult = "reviewable-result";

    /// <summary>
    /// 终态会话缺少复盘证据。
    /// </summary>
    public const string MissingReviewEvidence = "missing-review-evidence";
}

/// <summary>
/// Swarm 会话复盘快照。
/// </summary>
public sealed record SwarmSessionReviewSnapshot
{
    /// <summary>
    /// 会话状态。
    /// </summary>
    public SwarmStatus? SessionStatus { get; init; }

    /// <summary>
    /// 工作包总数。
    /// </summary>
    public int TotalPackageCount { get; init; }

    /// <summary>
    /// 失败工作包数量。
    /// </summary>
    public int FailedPackageCount { get; init; }

    /// <summary>
    /// 未完成工作包数量。
    /// </summary>
    public int NonTerminalPackageCount { get; init; }

    /// <summary>
    /// 可重试失败工作包数量。
    /// </summary>
    public int RetryablePackageCount { get; init; }

    /// <summary>
    /// 是否可恢复。
    /// </summary>
    public bool Recoverable { get; init; }

    /// <summary>
    /// 是否可复盘。
    /// </summary>
    public bool Reviewable { get; init; }

    /// <summary>
    /// 是否存在阻塞中的工作包。
    /// </summary>
    public bool HasBlockingPackages { get; init; }

    /// <summary>
    /// 是否具备结果摘要。
    /// </summary>
    public bool HasResultSummary { get; init; }

    /// <summary>
    /// 是否具备执行报告 Artifact。
    /// </summary>
    public bool HasExecutionReportArtifact { get; init; }

    /// <summary>
    /// 是否具备失败原因证据。
    /// </summary>
    public bool HasFailureEvidence { get; init; }

    /// <summary>
    /// 下一步建议动作。
    /// </summary>
    public string NextAction { get; init; } = SwarmSessionReviewActions.Refresh;

    /// <summary>
    /// 复盘原因。
    /// </summary>
    public string Reason { get; init; } = SwarmSessionReviewReasons.MissingSession;

    /// <summary>
    /// 第一个失败工作包标识。
    /// </summary>
    public string? FirstFailedPackageId { get; init; }
}

/// <summary>
/// Swarm 会话复盘动作。
/// </summary>
public static class SwarmSessionReviewActions
{
    /// <summary>
    /// 刷新状态。
    /// </summary>
    public const string Refresh = "Refresh";

    /// <summary>
    /// 重试失败工作包。
    /// </summary>
    public const string RetryFailedPackage = "RetryFailedPackage";

    /// <summary>
    /// 等待执行完成。
    /// </summary>
    public const string WaitForPackages = "WaitForPackages";

    /// <summary>
    /// 查看复盘结果。
    /// </summary>
    public const string ReviewResult = "ReviewResult";
}

/// <summary>
/// Swarm 会话复盘策略。
/// </summary>
public static class SwarmSessionReviewPolicy
{
    /// <summary>
    /// 根据会话和工作包状态构建复盘快照。
    /// </summary>
    public static SwarmSessionReviewSnapshot Build(ContextSwarmSession? session)
    {
        if (session == null)
        {
            return new SwarmSessionReviewSnapshot();
        }

        var packages = session.Packages.ToList();
        var failedPackages = packages
            .Where(package => package.Status == SwarmTaskStatus.Failed)
            .ToList();
        var nonTerminalCount = packages.Count(package => !IsTerminalPackageStatus(package.Status));
        var retryableCount = session.Status is not SwarmStatus.Aborted and not SwarmStatus.Completed
            ? failedPackages.Count
            : 0;
        var hasResultSummary = !string.IsNullOrWhiteSpace(session.Result)
            || packages.Any(package => !string.IsNullOrWhiteSpace(package.Result));
        var hasExecutionReportArtifact = packages.Any(package => package.ExecutionReportArtifactId.HasValue);
        var hasFailureEvidence = failedPackages.Any(package => !string.IsNullOrWhiteSpace(package.FailureReason));
        var hasBlockingPackages = nonTerminalCount > 0;
        var hasReviewEvidence = hasResultSummary || hasExecutionReportArtifact || hasFailureEvidence;

        return new SwarmSessionReviewSnapshot
        {
            SessionStatus = session.Status,
            TotalPackageCount = packages.Count,
            FailedPackageCount = failedPackages.Count,
            NonTerminalPackageCount = nonTerminalCount,
            RetryablePackageCount = retryableCount,
            Recoverable = retryableCount > 0,
            Reviewable = retryableCount == 0
                && IsTerminalSessionStatus(session.Status)
                && nonTerminalCount == 0
                && hasReviewEvidence,
            HasBlockingPackages = hasBlockingPackages,
            HasResultSummary = hasResultSummary,
            HasExecutionReportArtifact = hasExecutionReportArtifact,
            HasFailureEvidence = hasFailureEvidence,
            NextAction = ResolveNextAction(retryableCount, hasBlockingPackages, session.Status, hasReviewEvidence),
            Reason = ResolveReason(retryableCount, hasBlockingPackages, session.Status, hasReviewEvidence),
            FirstFailedPackageId = failedPackages.FirstOrDefault()?.TaskId
        };
    }

    private static string ResolveNextAction(
        int retryableCount,
        bool hasBlockingPackages,
        SwarmStatus status,
        bool hasReviewEvidence)
    {
        if (retryableCount > 0)
        {
            return SwarmSessionReviewActions.RetryFailedPackage;
        }

        if (hasBlockingPackages)
        {
            return SwarmSessionReviewActions.WaitForPackages;
        }

        return IsTerminalSessionStatus(status) && hasReviewEvidence
            ? SwarmSessionReviewActions.ReviewResult
            : SwarmSessionReviewActions.Refresh;
    }

    private static string ResolveReason(
        int retryableCount,
        bool hasBlockingPackages,
        SwarmStatus status,
        bool hasReviewEvidence)
    {
        if (retryableCount > 0)
        {
            return SwarmSessionReviewReasons.FailedPackagesRetryable;
        }

        if (hasBlockingPackages)
        {
            return SwarmSessionReviewReasons.NonTerminalPackages;
        }

        return IsTerminalSessionStatus(status) && hasReviewEvidence
            ? SwarmSessionReviewReasons.ReviewableResult
            : SwarmSessionReviewReasons.MissingReviewEvidence;
    }

    private static bool IsTerminalSessionStatus(SwarmStatus status)
    {
        return status is SwarmStatus.Completed or SwarmStatus.Failed or SwarmStatus.Aborted;
    }

    private static bool IsTerminalPackageStatus(SwarmTaskStatus status)
    {
        return status is SwarmTaskStatus.Completed
            or SwarmTaskStatus.Failed
            or SwarmTaskStatus.Skipped
            or SwarmTaskStatus.Transferred;
    }
}
