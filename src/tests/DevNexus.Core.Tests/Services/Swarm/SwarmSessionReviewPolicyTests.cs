using DevNexus.Core.Services.Swarm;
using DevNexus.Domain.Entities;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Swarm;

/// <summary>
/// Swarm 会话复盘策略测试。
/// </summary>
public sealed class SwarmSessionReviewPolicyTests
{
    /// <summary>
    /// 失败会话中的失败工作包应可解释为可恢复。
    /// </summary>
    [Fact]
    public void Build_ShouldMarkRecoverable_WhenFailedPackageCanRetry()
    {
        var session = CreateSession(
            SwarmStatus.Failed,
            null,
            CreatePackage(SwarmTaskStatus.Failed, failureReason: "执行失败"),
            CreatePackage(SwarmTaskStatus.Completed, result: "已完成"));

        var snapshot = SwarmSessionReviewPolicy.Build(session);

        snapshot.Recoverable.Should().BeTrue();
        snapshot.Reviewable.Should().BeFalse();
        snapshot.RetryablePackageCount.Should().Be(1);
        snapshot.FirstFailedPackageId.Should().NotBeNullOrWhiteSpace();
        snapshot.NextAction.Should().Be(SwarmSessionReviewActions.RetryFailedPackage);
        snapshot.Reason.Should().Be(SwarmSessionReviewReasons.FailedPackagesRetryable);
    }

    /// <summary>
    /// 运行中会话存在未完成工作包时应建议等待。
    /// </summary>
    [Fact]
    public void Build_ShouldMarkBlockingPackages_WhenPackagesAreNotTerminal()
    {
        var session = CreateSession(
            SwarmStatus.Running,
            null,
            CreatePackage(SwarmTaskStatus.InProgress),
            CreatePackage(SwarmTaskStatus.Pending));

        var snapshot = SwarmSessionReviewPolicy.Build(session);

        snapshot.HasBlockingPackages.Should().BeTrue();
        snapshot.NonTerminalPackageCount.Should().Be(2);
        snapshot.Recoverable.Should().BeFalse();
        snapshot.Reviewable.Should().BeFalse();
        snapshot.NextAction.Should().Be(SwarmSessionReviewActions.WaitForPackages);
        snapshot.Reason.Should().Be(SwarmSessionReviewReasons.NonTerminalPackages);
    }

    /// <summary>
    /// 已完成会话具备结果证据时应可复盘。
    /// </summary>
    [Fact]
    public void Build_ShouldMarkReviewable_WhenTerminalSessionHasEvidence()
    {
        var session = CreateSession(
            SwarmStatus.Completed,
            "整体完成",
            CreatePackage(SwarmTaskStatus.Completed, result: "输出结果"));

        var snapshot = SwarmSessionReviewPolicy.Build(session);

        snapshot.Reviewable.Should().BeTrue();
        snapshot.Recoverable.Should().BeFalse();
        snapshot.HasResultSummary.Should().BeTrue();
        snapshot.NextAction.Should().Be(SwarmSessionReviewActions.ReviewResult);
        snapshot.Reason.Should().Be(SwarmSessionReviewReasons.ReviewableResult);
    }

    /// <summary>
    /// 终态会话具备执行报告 Artifact 时，即使没有文本结果也应可复盘。
    /// </summary>
    [Fact]
    public void Build_ShouldMarkReviewable_WhenTerminalSessionHasExecutionReportArtifact()
    {
        var session = CreateSession(
            SwarmStatus.Completed,
            null,
            CreatePackage(SwarmTaskStatus.Completed, executionReportArtifactId: Guid.NewGuid()));

        var snapshot = SwarmSessionReviewPolicy.Build(session);

        snapshot.Reviewable.Should().BeTrue();
        snapshot.Recoverable.Should().BeFalse();
        snapshot.HasResultSummary.Should().BeFalse();
        snapshot.HasExecutionReportArtifact.Should().BeTrue();
        snapshot.NextAction.Should().Be(SwarmSessionReviewActions.ReviewResult);
        snapshot.Reason.Should().Be(SwarmSessionReviewReasons.ReviewableResult);
    }

    /// <summary>
    /// 终态会话具备失败原因时，即使没有结果摘要和执行报告也应可复盘。
    /// </summary>
    [Fact]
    public void Build_ShouldMarkReviewable_WhenTerminalSessionHasFailureEvidence()
    {
        var session = CreateSession(
            SwarmStatus.Aborted,
            null,
            CreatePackage(SwarmTaskStatus.Failed, failureReason: "用户中止前执行失败"));

        var snapshot = SwarmSessionReviewPolicy.Build(session);

        snapshot.Reviewable.Should().BeTrue();
        snapshot.Recoverable.Should().BeFalse();
        snapshot.HasResultSummary.Should().BeFalse();
        snapshot.HasExecutionReportArtifact.Should().BeFalse();
        snapshot.HasFailureEvidence.Should().BeTrue();
        snapshot.NextAction.Should().Be(SwarmSessionReviewActions.ReviewResult);
        snapshot.Reason.Should().Be(SwarmSessionReviewReasons.ReviewableResult);
    }

    /// <summary>
    /// 终态会话缺少结果与执行报告时应标记证据不足。
    /// </summary>
    [Fact]
    public void Build_ShouldMarkMissingEvidence_WhenTerminalSessionHasNoReviewFacts()
    {
        var session = CreateSession(
            SwarmStatus.Aborted,
            null,
            CreatePackage(SwarmTaskStatus.Skipped));

        var snapshot = SwarmSessionReviewPolicy.Build(session);

        snapshot.Reviewable.Should().BeFalse();
        snapshot.HasResultSummary.Should().BeFalse();
        snapshot.HasExecutionReportArtifact.Should().BeFalse();
        snapshot.HasFailureEvidence.Should().BeFalse();
        snapshot.NextAction.Should().Be(SwarmSessionReviewActions.Refresh);
        snapshot.Reason.Should().Be(SwarmSessionReviewReasons.MissingReviewEvidence);
    }

    private static ContextSwarmSession CreateSession(
        SwarmStatus status,
        string? result,
        params ContextWorkPackageRecord[] packages)
    {
        return new ContextSwarmSession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            Status = status,
            Result = result,
            Packages = packages.ToList()
        };
    }

    private static ContextWorkPackageRecord CreatePackage(
        SwarmTaskStatus status,
        string? result = null,
        string? failureReason = null,
        Guid? executionReportArtifactId = null)
    {
        return new ContextWorkPackageRecord
        {
            TaskId = Guid.NewGuid().ToString("N"),
            Status = status,
            Result = result,
            FailureReason = failureReason,
            ExecutionReportArtifactId = executionReportArtifactId
        };
    }
}
