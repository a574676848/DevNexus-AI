using DevNexus.Shared.DTOs;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Shared;

/// <summary>
/// Token 使用量派生指标测试。
/// </summary>
public sealed class TokenUsageMetricsTests
{
    /// <summary>
    /// 非缓存输入 Token 应从输入 Token 中扣除缓存命中部分。
    /// </summary>
    [Fact]
    public void CalculateNonCachedInputTokens_ShouldSubtractCachedPromptTokens()
    {
        var nonCached = TokenUsageMetrics.CalculateNonCachedInputTokens((int?)100, 40);

        nonCached.Should().Be(60);
    }

    /// <summary>
    /// 缓存命中大于输入 Token 时应归零。
    /// </summary>
    [Fact]
    public void CalculateNonCachedInputTokens_ShouldClampToZero()
    {
        var nonCached = TokenUsageMetrics.CalculateNonCachedInputTokens((int?)20, 40);

        nonCached.Should().Be(0);
    }

    /// <summary>
    /// 缺少输入 Token 时派生值应保持为空。
    /// </summary>
    [Fact]
    public void CalculateNonCachedInputTokens_ShouldReturnNull_WhenInputMissing()
    {
        var nonCached = TokenUsageMetrics.CalculateNonCachedInputTokens(null, 40);

        nonCached.Should().BeNull();
    }
}
