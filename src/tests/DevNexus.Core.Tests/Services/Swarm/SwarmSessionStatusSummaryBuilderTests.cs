using DevNexus.Core.Services.Swarm;
using DevNexus.Shared.DTOs.Swarm;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Swarm;

/// <summary>
/// Swarm 会话状态摘要构建器测试。
/// </summary>
public sealed class SwarmSessionStatusSummaryBuilderTests
{
    /// <summary>
    /// 空工作包应显示等待状态。
    /// </summary>
    [Fact]
    public void Build_ShouldReturnNeutral_WhenPackagesEmpty()
    {
        var summary = SwarmSessionStatusSummaryBuilder.Build(Array.Empty<ContextWorkPackageDto>(), isPaused: false);

        summary.Tone.Should().Be("neutral");
        summary.Label.Should().Be("等待工作包进入 Swarm");
        summary.TotalCount.Should().Be(0);
        summary.IsTerminal.Should().BeFalse();
    }

    /// <summary>
    /// 失败工作包优先于暂停状态展示。
    /// </summary>
    [Fact]
    public void Build_ShouldPrioritizeFailure_WhenFailedPackageExists()
    {
        var summary = SwarmSessionStatusSummaryBuilder.Build(
            new[] { CreatePackage(SwarmTaskStatusNames.Failed), CreatePackage(SwarmTaskStatusNames.InProgress) },
            isPaused: true);

        summary.Tone.Should().Be("danger");
        summary.Label.Should().Be("存在失败工作包");
        summary.FailedCount.Should().Be(1);
        summary.HasFailures.Should().BeTrue();
        summary.IsPaused.Should().BeTrue();
    }

    /// <summary>
    /// 暂停状态应在无失败时展示。
    /// </summary>
    [Fact]
    public void Build_ShouldReturnPaused_WhenSessionPaused()
    {
        var summary = SwarmSessionStatusSummaryBuilder.Build(
            new[] { CreatePackage(SwarmTaskStatusNames.Pending) },
            isPaused: true);

        summary.Tone.Should().Be("warning");
        summary.Label.Should().Be("Swarm 已暂停");
        summary.IsPaused.Should().BeTrue();
    }

    /// <summary>
    /// 执行中工作包应生成 active 摘要。
    /// </summary>
    [Fact]
    public void Build_ShouldReturnActive_WhenPackageExecuting()
    {
        var summary = SwarmSessionStatusSummaryBuilder.Build(
            new[] { CreatePackage(SwarmTaskStatusNames.InProgress), CreatePackage(SwarmTaskStatusNames.Pending) },
            isPaused: false);

        summary.Tone.Should().Be("active");
        summary.Label.Should().Be("Swarm 正在执行");
        summary.ExecutingCount.Should().Be(1);
    }

    /// <summary>
    /// 评估或重试中的工作包应生成 warning 摘要。
    /// </summary>
    [Fact]
    public void Build_ShouldReturnWarning_WhenPackageEvaluating()
    {
        var summary = SwarmSessionStatusSummaryBuilder.Build(
            new[] { CreatePackage(SwarmTaskStatusNames.Retrying) },
            isPaused: false);

        summary.Tone.Should().Be("warning");
        summary.Label.Should().Be("Swarm 正在评估");
        summary.EvaluatingCount.Should().Be(1);
    }

    /// <summary>
    /// 所有工作包终态时应显示收尾。
    /// </summary>
    [Fact]
    public void Build_ShouldReturnSuccess_WhenAllPackagesTerminal()
    {
        var summary = SwarmSessionStatusSummaryBuilder.Build(
            new[] { CreatePackage(SwarmTaskStatusNames.Completed), CreatePackage(SwarmTaskStatusNames.Skipped) },
            isPaused: false);

        summary.Tone.Should().Be("success");
        summary.Label.Should().Be("Swarm 已收尾");
        summary.TerminalCount.Should().Be(2);
        summary.IsTerminal.Should().BeTrue();
    }

    /// <summary>
    /// 阶段指标应按共享状态语义计数。
    /// </summary>
    [Fact]
    public void Build_ShouldReturnStageMetrics()
    {
        var summary = SwarmSessionStatusSummaryBuilder.Build(
            new[]
            {
                CreatePackage(SwarmTaskStatusNames.Pending),
                CreatePackage("Ready"),
                CreatePackage(SwarmTaskStatusNames.GroupChatting),
                CreatePackage(SwarmTaskStatusNames.Evaluating),
                CreatePackage(SwarmTaskStatusNames.Completed)
            },
            isPaused: false);

        summary.PlanningCount.Should().Be(2);
        summary.ExecutingCount.Should().Be(1);
        summary.EvaluatingCount.Should().Be(1);
        summary.TerminalCount.Should().Be(1);
        summary.StageMetrics.Select(metric => metric.Count).Should().Equal(2, 1, 1, 1);
    }

    private static ContextWorkPackageDto CreatePackage(string status)
    {
        return new ContextWorkPackageDto
        {
            Id = Guid.NewGuid().ToString("N"),
            Status = status
        };
    }
}
