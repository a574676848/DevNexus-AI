using DevNexus.Core.DTOs;
using DevNexus.Core.Services.Chat;
using DevNexus.Domain.Entities;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 系统经验回放快照测试。
/// </summary>
public sealed class SystemExperienceReplaySnapshotTests
{
    /// <summary>
    /// 空决策应生成空快照。
    /// </summary>
    [Fact]
    public void FromDecision_ShouldReturnEmpty_WhenDecisionIsNull()
    {
        var snapshot = SystemExperienceReplaySnapshot.FromDecision(null);

        snapshot.HasMatch.Should().BeFalse();
        snapshot.Reason.Should().Be(SystemExperienceReplayReasons.NoMatch);
    }

    /// <summary>
    /// 回放决策应转换为结构化快照。
    /// </summary>
    [Fact]
    public void FromDecision_ShouldCreateSnapshot_WhenMatchExists()
    {
        var experienceId = Guid.NewGuid();
        var decision = SystemExperienceReplayPolicy.Decide(new ExperienceMatchDto
        {
            Similarity = 0.88f,
            Experience = new SystemExperience
            {
                Id = experienceId,
                Type = ExperienceType.QA,
                Intent = "修复构建失败",
                SolutionSop = "运行构建。",
                ContextTags = ExperienceDistillationOutputProtocol.ContextTag
            }
        });

        var snapshot = SystemExperienceReplaySnapshot.FromDecision(decision);

        snapshot.HasMatch.Should().BeTrue();
        snapshot.WasReplayed.Should().BeTrue();
        snapshot.InjectedDynamicContext.Should().BeTrue();
        snapshot.ExperienceId.Should().Be(experienceId);
        snapshot.Similarity.Should().Be(0.88f);
        snapshot.ContextTags.Should().Be(ExperienceDistillationOutputProtocol.ContextTag);
        snapshot.ContextTagSnapshot.HasDistillationProtocol.Should().BeTrue();
    }

    /// <summary>
    /// 直接返回决策应在快照中保留直接命中事实。
    /// </summary>
    [Fact]
    public void FromDecision_ShouldMarkDirectAnswer_WhenPerfectHit()
    {
        var decision = SystemExperienceReplayPolicy.Decide(new ExperienceMatchDto
        {
            Similarity = 0.96f,
            Experience = new SystemExperience
            {
                Id = Guid.NewGuid(),
                Type = ExperienceType.QA,
                Intent = "复用经验",
                SolutionSop = "直接返回。"
            }
        });

        var snapshot = SystemExperienceReplaySnapshot.FromDecision(decision);

        snapshot.HasMatch.Should().BeTrue();
        snapshot.WasReplayed.Should().BeTrue();
        snapshot.AnsweredDirectly.Should().BeTrue();
        snapshot.InjectedDynamicContext.Should().BeFalse();
        snapshot.Reason.Should().Be(SystemExperienceReplayReasons.DirectAnswer);
    }

    /// <summary>
    /// 回放快照应保留系统经验中的自我迭代调度事实。
    /// </summary>
    [Fact]
    public void FromDecision_ShouldParseSelfIterationFactsFromContextTags()
    {
        var sourceSessionId = Guid.NewGuid();
        var decision = SystemExperienceReplayPolicy.Decide(new ExperienceMatchDto
        {
            Similarity = 0.96f,
            Experience = new SystemExperience
            {
                Id = Guid.NewGuid(),
                Type = ExperienceType.QA,
                Intent = "复用上下文治理经验",
                SolutionSop = "直接返回。",
                ContextTags = string.Join(
                    ",",
                    ExperienceDistillationOutputProtocol.ContextTag,
                    ExperienceDistillationOutputProtocol.CandidateReasonTagPrefix + "memory-consolidation-immediate",
                    ExperienceDistillationOutputProtocol.ContextPressureReasonTagPrefix + "summary-compression",
                    ExperienceDistillationOutputProtocol.ContextCompressionFingerprintTagPrefix + "fingerprint",
                    ExperienceDistillationOutputProtocol.ValueSignalTagPrefix + "流程",
                    ExperienceDistillationOutputProtocol.SourceSessionTagPrefix + sourceSessionId.ToString("D"))
            }
        });

        var snapshot = SystemExperienceReplaySnapshot.FromDecision(decision);

        snapshot.ContextTagSnapshot.HasSelfIterationFacts.Should().BeTrue();
        snapshot.ContextTagSnapshot.CandidateReason.Should().Be("memory-consolidation-immediate");
        snapshot.ContextTagSnapshot.ContextPressureReason.Should().Be("summary-compression");
        snapshot.ContextTagSnapshot.ContextCompressionSummaryFingerprint.Should().Be("fingerprint");
        snapshot.ValueSignalKeyword.Should().Be("流程");
        snapshot.SourceSessionId.Should().Be(sourceSessionId);
        snapshot.MemoryCitation.ExperienceId.Should().Be(snapshot.ExperienceId);
        snapshot.MemoryCitation.SourceSessionId.Should().Be(sourceSessionId);
        snapshot.MemoryCitation.ValueSignalKeyword.Should().Be("流程");
    }
}
