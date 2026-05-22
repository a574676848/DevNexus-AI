using DevNexus.Core.DTOs;
using DevNexus.Core.Services.Chat;
using DevNexus.Domain.Entities;
using DevNexus.Shared.Constants;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 系统经验回放策略测试。
/// </summary>
public sealed class SystemExperienceReplayPolicyTests
{
    /// <summary>
    /// 未命中时不应回放系统经验。
    /// </summary>
    [Fact]
    public void Decide_ShouldSkipReplay_WhenMatchIsNull()
    {
        var decision = SystemExperienceReplayPolicy.Decide(null);

        decision.ShouldAnswerDirectly.Should().BeFalse();
        decision.ShouldInjectDynamicContext.Should().BeFalse();
        decision.Reason.Should().Be(SystemExperienceReplayReasons.NoMatch);
    }

    /// <summary>
    /// 完全命中时应直接返回系统经验答案。
    /// </summary>
    [Fact]
    public void Decide_ShouldAnswerDirectly_WhenPerfectHit()
    {
        var decision = SystemExperienceReplayPolicy.Decide(
            CreateMatch(MemoryConstants.ChatPerfectHitThreshold));

        decision.ShouldAnswerDirectly.Should().BeTrue();
        decision.ShouldInjectDynamicContext.Should().BeFalse();
        decision.Reason.Should().Be(SystemExperienceReplayReasons.DirectAnswer);
    }

    /// <summary>
    /// 部分命中时应注入动态上下文。
    /// </summary>
    [Fact]
    public void Decide_ShouldInjectDynamicContext_WhenPartialHit()
    {
        var decision = SystemExperienceReplayPolicy.Decide(
            CreateMatch(MemoryConstants.ChatPartialHitThreshold));

        decision.ShouldAnswerDirectly.Should().BeFalse();
        decision.ShouldInjectDynamicContext.Should().BeTrue();
        decision.Reason.Should().Be(SystemExperienceReplayReasons.DynamicContext);
    }

    /// <summary>
    /// 低于部分命中阈值时不应回放。
    /// </summary>
    [Fact]
    public void Decide_ShouldSkipReplay_WhenBelowPartialThreshold()
    {
        var decision = SystemExperienceReplayPolicy.Decide(
            CreateMatch(MemoryConstants.ChatPartialHitThreshold - 0.01f));

        decision.ShouldAnswerDirectly.Should().BeFalse();
        decision.ShouldInjectDynamicContext.Should().BeFalse();
        decision.Reason.Should().Be(SystemExperienceReplayReasons.BelowReplayThreshold);
    }

    private static ExperienceMatchDto CreateMatch(float similarity)
    {
        return new ExperienceMatchDto
        {
            Similarity = similarity,
            Experience = new SystemExperience
            {
                Type = ExperienceType.QA,
                Intent = "修复构建失败",
                SolutionSop = "运行构建并分析错误。"
            }
        };
    }
}
