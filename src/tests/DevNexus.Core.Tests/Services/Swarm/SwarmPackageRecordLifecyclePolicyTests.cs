using DevNexus.Core.Services.Swarm;
using DevNexus.Domain.Entities;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Swarm;

/// <summary>
/// Swarm 工作包记录生命周期策略测试。
/// </summary>
public sealed class SwarmPackageRecordLifecyclePolicyTests
{
    /// <summary>
    /// 失败工作包应补齐完成时间，保证重试失败后也能进入闭环终态。
    /// </summary>
    [Fact]
    public void Apply_ShouldSetCompletedAt_WhenPackageFailed()
    {
        var now = DateTime.UtcNow;
        var record = new ContextWorkPackageRecord
        {
            Status = SwarmTaskStatus.Failed
        };

        SwarmPackageRecordLifecyclePolicy.Apply(record, now);

        record.StartedAt.Should().Be(now);
        record.CompletedAt.Should().Be(now);
        record.UpdatedAt.Should().Be(now);
    }

    /// <summary>
    /// 跳过工作包应补齐完成时间，保证中止后的未完成包可被识别为终态。
    /// </summary>
    [Fact]
    public void Apply_ShouldSetCompletedAt_WhenPackageSkipped()
    {
        var now = DateTime.UtcNow;
        var record = new ContextWorkPackageRecord
        {
            Status = SwarmTaskStatus.Skipped
        };

        SwarmPackageRecordLifecyclePolicy.Apply(record, now);

        record.CompletedAt.Should().Be(now);
    }

    /// <summary>
    /// 工作包重新进入执行态时应清空旧完成时间。
    /// </summary>
    [Fact]
    public void Apply_ShouldClearCompletedAt_WhenPackageBecomesActiveAgain()
    {
        var now = DateTime.UtcNow;
        var oldCompletedAt = now.AddMinutes(-5);
        var record = new ContextWorkPackageRecord
        {
            Status = SwarmTaskStatus.InProgress,
            StartedAt = now.AddMinutes(-10),
            CompletedAt = oldCompletedAt
        };

        SwarmPackageRecordLifecyclePolicy.Apply(record, now);

        record.StartedAt.Should().NotBe(now);
        record.CompletedAt.Should().BeNull();
        record.UpdatedAt.Should().Be(now);
    }
}
