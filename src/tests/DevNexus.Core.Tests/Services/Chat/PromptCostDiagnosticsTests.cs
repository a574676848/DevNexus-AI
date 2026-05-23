using DevNexus.Core.Services.Chat;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// Prompt 成本诊断工具测试。
/// </summary>
public sealed class PromptCostDiagnosticsTests
{
    /// <summary>
    /// 成本诊断应派生缓存命中率和上下文占比。
    /// </summary>
    [Fact]
    public void Build_ShouldCalculateCacheAndContextRatios()
    {
        var snapshot = PromptCostDiagnostics.Build(new PromptCostObservation
        {
            InputTokens = 1000,
            CachedPromptTokens = 400,
            DynamicContextTokens = 150,
            HistoryTokens = 250
        });

        snapshot.NonCachedInputTokens.Should().Be(600);
        snapshot.CacheHitRatio.Should().Be(0.4m);
        snapshot.DynamicContextRatio.Should().Be(0.15m);
        snapshot.HistoryRatio.Should().Be(0.25m);
    }

    /// <summary>
    /// 缓存命中 Token 超过输入 Token 时应按完整命中封顶。
    /// </summary>
    [Fact]
    public void Build_ShouldClampCacheHitRatio()
    {
        var snapshot = PromptCostDiagnostics.Build(new PromptCostObservation
        {
            InputTokens = 200,
            CachedPromptTokens = 300
        });

        snapshot.NonCachedInputTokens.Should().Be(0);
        snapshot.CacheHitRatio.Should().Be(1m);
    }

    /// <summary>
    /// Provider 返回负数 Token 观测值时应按 0 处理。
    /// </summary>
    [Fact]
    public void Build_ShouldNormalizeNegativeTokenObservations()
    {
        var snapshot = PromptCostDiagnostics.Build(new PromptCostObservation
        {
            InputTokens = 100,
            CachedPromptTokens = -10,
            DynamicContextTokens = -20,
            HistoryTokens = -30
        });

        snapshot.NonCachedInputTokens.Should().Be(100);
        snapshot.CacheHitRatio.Should().Be(0m);
        snapshot.DynamicContextRatio.Should().Be(0m);
        snapshot.HistoryRatio.Should().Be(0m);
    }

    /// <summary>
    /// 缺少输入 Token 时比例派生值应保持为空。
    /// </summary>
    [Fact]
    public void Build_ShouldReturnNullRatios_WhenInputTokensMissing()
    {
        var snapshot = PromptCostDiagnostics.Build(new PromptCostObservation
        {
            CachedPromptTokens = 100,
            DynamicContextTokens = 50,
            HistoryTokens = 50
        });

        snapshot.NonCachedInputTokens.Should().BeNull();
        snapshot.CacheHitRatio.Should().BeNull();
        snapshot.DynamicContextRatio.Should().BeNull();
        snapshot.HistoryRatio.Should().BeNull();
    }
}
