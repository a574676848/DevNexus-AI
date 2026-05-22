using DevNexus.Core.Services.Swarm;
using DevNexus.Domain.Entities;
using DevNexus.Domain.Enums;
using DevNexus.Domain.Models.Swarm;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Swarm;

/// <summary>
/// Swarm 会话收尾策略测试。
/// </summary>
public sealed class SwarmSessionFinalizationPolicyTests
{
    /// <summary>
    /// 取消收尾应只中止未完成工作包。
    /// </summary>
    [Fact]
    public void BuildCancelled_ShouldAbortUnfinishedPackages()
    {
        var packages = CreatePackages();

        var result = SwarmSessionFinalizationPolicy.BuildCancelled(packages);

        result.Status.Should().Be(SwarmStatus.Aborted);
        result.NotifyCancellation.Should().BeTrue();
        result.NotifyFailure.Should().BeFalse();
        packages[0].Status.Should().Be(SwarmPackageStatus.Completed);
        packages[1].Status.Should().Be(SwarmPackageStatus.Aborted);
        packages[1].FailureReason.Should().Be("Swarm 执行已取消。");
    }

    /// <summary>
    /// 失败收尾应保留已完成包，并给未完成包补失败原因。
    /// </summary>
    [Fact]
    public void BuildFailed_ShouldFailUnfinishedPackages()
    {
        var packages = CreatePackages();

        var result = SwarmSessionFinalizationPolicy.BuildFailed(
            packages,
            new InvalidOperationException("执行器异常"));

        result.Status.Should().Be(SwarmStatus.Failed);
        result.NotifyFailure.Should().BeTrue();
        result.NotifyCancellation.Should().BeFalse();
        result.Reason.Should().Be("执行器异常");
        packages[0].Status.Should().Be(SwarmPackageStatus.Completed);
        packages[1].Status.Should().Be(SwarmPackageStatus.Failed);
        packages[1].Result.Should().Be("执行器异常");
    }

    /// <summary>
    /// 启动恢复收尾应补齐持久化工作包终态。
    /// </summary>
    [Fact]
    public void BuildInterruptedRecovery_ShouldFailUnfinishedPackageRecords()
    {
        var records = new List<ContextWorkPackageRecord>
        {
            new()
            {
                TaskId = "done",
                Title = "已完成",
                Status = SwarmTaskStatus.Completed,
                Result = "ok"
            },
            new()
            {
                TaskId = "running",
                Title = "执行中",
                Status = SwarmTaskStatus.InProgress
            },
            new()
            {
                TaskId = "ready",
                Title = "待执行",
                Status = SwarmTaskStatus.Ready
            }
        };

        var result = SwarmSessionFinalizationPolicy.BuildInterruptedRecovery(records, "服务重启中断");

        result.Status.Should().Be(SwarmStatus.Failed);
        result.NotifyFailure.Should().BeTrue();
        records[0].Status.Should().Be(SwarmTaskStatus.Completed);
        records[0].Result.Should().Be("ok");
        records[1].Status.Should().Be(SwarmTaskStatus.Failed);
        records[1].FailureReason.Should().Be("服务重启中断");
        records[1].CompletedAt.Should().NotBeNull();
        records[2].Status.Should().Be(SwarmTaskStatus.Failed);
        records[2].Result.Should().Be("服务重启中断");
    }

    /// <summary>
    /// 用户主动取消应将未完成的持久化工作包标记为已跳过。
    /// </summary>
    [Fact]
    public void BuildUserAbort_ShouldSkipUnfinishedPackageRecords()
    {
        var records = new List<ContextWorkPackageRecord>
        {
            new()
            {
                TaskId = "done",
                Title = "已完成",
                Status = SwarmTaskStatus.Completed,
                Result = "ok"
            },
            new()
            {
                TaskId = "running",
                Title = "执行中",
                Status = SwarmTaskStatus.InProgress
            },
            new()
            {
                TaskId = "pending",
                Title = "待执行",
                Status = SwarmTaskStatus.Pending
            }
        };

        var result = SwarmSessionFinalizationPolicy.BuildUserAbort(records);

        result.Status.Should().Be(SwarmStatus.Aborted);
        result.NotifyCancellation.Should().BeTrue();
        result.NotifyFailure.Should().BeFalse();
        records[0].Status.Should().Be(SwarmTaskStatus.Completed);
        records[0].Result.Should().Be("ok");
        records[1].Status.Should().Be(SwarmTaskStatus.Skipped);
        records[1].FailureReason.Should().Be("Swarm 执行已取消。");
        records[1].CompletedAt.Should().NotBeNull();
        records[2].Status.Should().Be(SwarmTaskStatus.Skipped);
        records[2].Result.Should().Be("Swarm 执行已取消。");
    }

    private static List<ContextWorkPackage> CreatePackages()
    {
        return new List<ContextWorkPackage>
        {
            new()
            {
                Id = "done",
                Title = "已完成",
                Status = SwarmPackageStatus.Completed,
                Result = "ok"
            },
            new()
            {
                Id = "running",
                Title = "执行中",
                Status = SwarmPackageStatus.InProgress
            }
        };
    }
}
