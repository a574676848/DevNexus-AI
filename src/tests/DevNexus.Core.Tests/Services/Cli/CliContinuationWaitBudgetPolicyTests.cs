using DevNexus.Core.Services.Cli;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Cli;

/// <summary>
/// CLI 续接等待预算策略测试。
/// </summary>
public sealed class CliContinuationWaitBudgetPolicyTests
{
    /// <summary>
    /// 等待预算低于下限时应提升到最小值。
    /// </summary>
    [Fact]
    public void Normalize_ShouldClampToMinimum()
    {
        var timeout = CliContinuationWaitBudgetPolicy.Normalize(100);

        timeout.Should().Be(TimeSpan.FromMilliseconds(CliContinuationWaitBudgetPolicy.MinimumWaitMilliseconds));
    }

    /// <summary>
    /// 等待预算高于上限时应收敛到最大值。
    /// </summary>
    [Fact]
    public void Normalize_ShouldClampToMaximum()
    {
        var timeout = CliContinuationWaitBudgetPolicy.Normalize(60000);

        timeout.Should().Be(TimeSpan.FromMilliseconds(CliContinuationWaitBudgetPolicy.MaximumWaitMilliseconds));
    }

    /// <summary>
    /// 合法等待预算应保持原值。
    /// </summary>
    [Fact]
    public void Normalize_ShouldKeepValidBudget()
    {
        var timeout = CliContinuationWaitBudgetPolicy.Normalize(5000);

        timeout.Should().Be(TimeSpan.FromMilliseconds(5000));
    }
}
