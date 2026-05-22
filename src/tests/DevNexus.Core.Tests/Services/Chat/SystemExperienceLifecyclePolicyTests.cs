using DevNexus.Core.Services.Chat;
using DevNexus.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 系统经验生命周期策略测试。
/// </summary>
public sealed class SystemExperienceLifecyclePolicyTests
{
    /// <summary>
    /// 相关度达到阈值时应视为经验命中。
    /// </summary>
    [Fact]
    public void IsSearchMatch_ShouldReturnTrue_WhenRelevanceMeetsThreshold()
    {
        SystemExperienceLifecyclePolicy
            .IsSearchMatch(SystemExperienceLifecyclePolicy.MinimumSearchRelevance)
            .Should()
            .BeTrue();
    }

    /// <summary>
    /// 命中增强不应超过效用评分上限。
    /// </summary>
    [Fact]
    public void BoostUtilityScore_ShouldClampToMaximumScore()
    {
        var boosted = SystemExperienceLifecyclePolicy.BoostUtilityScore(
            SystemExperienceLifecyclePolicy.MaximumUtilityScore);

        boosted.Should().Be(SystemExperienceLifecyclePolicy.MaximumUtilityScore);
    }

    /// <summary>
    /// 重复经验再次被发现时应强化已有经验。
    /// </summary>
    [Fact]
    public void ApplyDuplicateRediscovery_ShouldBoostExistingExperience()
    {
        var matchedAt = new DateTime(2026, 5, 22, 11, 0, 0, DateTimeKind.Utc);
        var experience = new SystemExperience
        {
            UsageCount = 2,
            UtilityScore = 5.0,
            LastMatchedAt = matchedAt.AddDays(-1)
        };

        SystemExperienceLifecyclePolicy.ApplyDuplicateRediscovery(experience, matchedAt);

        experience.UsageCount.Should().Be(3);
        experience.UtilityScore.Should().Be(5.0 + SystemExperienceLifecyclePolicy.BoostIncrement);
        experience.LastMatchedAt.Should().Be(matchedAt);
    }

    /// <summary>
    /// 过期边界应按统一天数回退。
    /// </summary>
    [Fact]
    public void GetStaleBoundary_ShouldUseConfiguredStaleDays()
    {
        var now = new DateTime(2026, 5, 22, 10, 0, 0, DateTimeKind.Utc);

        var boundary = SystemExperienceLifecyclePolicy.GetStaleBoundary(now);

        boundary.Should().Be(now.AddDays(-SystemExperienceLifecyclePolicy.StaleAfterDays));
    }

    /// <summary>
    /// 衰减后低于淘汰阈值时应标记为可删除。
    /// </summary>
    [Fact]
    public void ApplyDecay_ShouldReturnTrue_WhenUtilityDropsBelowPruneThreshold()
    {
        var experience = new SystemExperience
        {
            UtilityScore = SystemExperienceLifecyclePolicy.PruneBelowUtilityScore
        };

        var shouldPrune = SystemExperienceLifecyclePolicy.ApplyDecay(experience);

        shouldPrune.Should().BeTrue();
        experience.UtilityScore.Should().Be(
            SystemExperienceLifecyclePolicy.PruneBelowUtilityScore
            * SystemExperienceLifecyclePolicy.StaleDecayFactor);
    }
}
