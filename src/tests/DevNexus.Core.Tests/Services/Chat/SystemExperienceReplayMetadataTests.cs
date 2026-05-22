using DevNexus.Core.DTOs;
using DevNexus.Core.Services.Chat;
using DevNexus.Domain.Entities;
using DevNexus.Shared.Constants;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 系统经验回放 metadata 测试。
/// </summary>
public sealed class SystemExperienceReplayMetadataTests
{
    /// <summary>
    /// 动态上下文回放应写入统一系统经验字段。
    /// </summary>
    [Fact]
    public void Apply_ShouldWriteSystemExperienceMetadata()
    {
        var metadata = new Dictionary<string, object>();
        var decision = SystemExperienceReplayPolicy.Decide(CreateMatch(0.88f));

        SystemExperienceReplayMetadata.Apply(metadata, decision);

        metadata[ChatMessageMetadataKeys.SystemExperienceId].Should().Be(decision.Match!.Experience.Id);
        metadata[ChatMessageMetadataKeys.SystemExperienceSimilarity].Should().Be(0.88f);
        metadata[ChatMessageMetadataKeys.SystemExperienceReplayReason].Should().Be(SystemExperienceReplayReasons.DynamicContext);
        metadata[ChatMessageMetadataKeys.SystemExperienceContextTags].Should().Be(ExperienceDistillationOutputProtocol.ContextTag);
        metadata.Should().NotContainKey(ChatMessageMetadataKeys.CacheHit);
    }

    /// <summary>
    /// 直接命中回放应额外写入缓存命中字段。
    /// </summary>
    [Fact]
    public void ApplyDirectHit_ShouldWriteCacheHitMetadata()
    {
        var metadata = new Dictionary<string, object>();
        var decision = SystemExperienceReplayPolicy.Decide(CreateMatch(0.96f));

        SystemExperienceReplayMetadata.ApplyDirectHit(metadata, decision);

        metadata[ChatMessageMetadataKeys.CacheHit].Should().Be(true);
        metadata[ChatMessageMetadataKeys.Similarity].Should().Be(0.96f);
        metadata[ChatMessageMetadataKeys.SystemExperienceReplayReason].Should().Be(SystemExperienceReplayReasons.DirectAnswer);
    }

    /// <summary>
    /// 应能从 metadata 读取系统经验回放快照。
    /// </summary>
    [Fact]
    public void BuildSnapshot_ShouldReadSystemExperienceMetadata()
    {
        var metadata = new Dictionary<string, object>();
        var decision = SystemExperienceReplayPolicy.Decide(CreateMatch(0.88f));
        SystemExperienceReplayMetadata.Apply(metadata, decision);

        var snapshot = SystemExperienceReplayMetadata.BuildSnapshot(metadata);

        snapshot.HasMatch.Should().BeTrue();
        snapshot.InjectedDynamicContext.Should().BeTrue();
        snapshot.ExperienceId.Should().Be(decision.Match!.Experience.Id);
        snapshot.Similarity.Should().Be(0.88f);
        snapshot.ContextTags.Should().Be(ExperienceDistillationOutputProtocol.ContextTag);
        snapshot.ContextTagSnapshot.HasDistillationProtocol.Should().BeTrue();
    }

    /// <summary>
    /// 从 metadata 还原快照时应解析系统经验上下文标签事实。
    /// </summary>
    [Fact]
    public void BuildSnapshot_ShouldParseContextTagFacts()
    {
        var sourceSessionId = Guid.NewGuid();
        var metadata = new Dictionary<string, object>
        {
            [ChatMessageMetadataKeys.SystemExperienceReplayReason] = SystemExperienceReplayReasons.DynamicContext,
            [ChatMessageMetadataKeys.SystemExperienceContextTags] = string.Join(
                ",",
                ExperienceDistillationOutputProtocol.ContextTag,
                ExperienceDistillationOutputProtocol.CandidateReasonTagPrefix + "tool-workflow-completed",
                ExperienceDistillationOutputProtocol.ContextPressureReasonTagPrefix + "budget-truncated",
                ExperienceDistillationOutputProtocol.ContextCompressionFingerprintTagPrefix + "fingerprint",
                ExperienceDistillationOutputProtocol.ValueSignalTagPrefix + "决策",
                ExperienceDistillationOutputProtocol.SourceSessionTagPrefix + sourceSessionId.ToString("D"))
        };

        var snapshot = SystemExperienceReplayMetadata.BuildSnapshot(metadata);

        snapshot.ContextTagSnapshot.HasSelfIterationFacts.Should().BeTrue();
        snapshot.ContextTagSnapshot.CandidateReason.Should().Be("tool-workflow-completed");
        snapshot.ContextTagSnapshot.ContextPressureReason.Should().Be("budget-truncated");
        snapshot.ContextTagSnapshot.ContextCompressionSummaryFingerprint.Should().Be("fingerprint");
        snapshot.ValueSignalKeyword.Should().Be("决策");
        snapshot.SourceSessionId.Should().Be(sourceSessionId);
    }

    /// <summary>
    /// 直接命中 metadata 应读取为直接返回快照。
    /// </summary>
    [Fact]
    public void BuildSnapshot_ShouldReadDirectHitMode()
    {
        var metadata = new Dictionary<string, object>();
        var decision = SystemExperienceReplayPolicy.Decide(CreateMatch(0.96f));
        SystemExperienceReplayMetadata.ApplyDirectHit(metadata, decision);

        var snapshot = SystemExperienceReplayMetadata.BuildSnapshot(metadata);

        snapshot.WasReplayed.Should().BeTrue();
        snapshot.AnsweredDirectly.Should().BeTrue();
        snapshot.InjectedDynamicContext.Should().BeFalse();
        snapshot.Reason.Should().Be(SystemExperienceReplayReasons.DirectAnswer);
    }

    /// <summary>
    /// 无回放 metadata 时应返回空快照。
    /// </summary>
    [Fact]
    public void BuildSnapshot_ShouldReturnEmpty_WhenMetadataIsMissing()
    {
        var snapshot = SystemExperienceReplayMetadata.BuildSnapshot(new Dictionary<string, object>());

        snapshot.HasMatch.Should().BeFalse();
        snapshot.WasReplayed.Should().BeFalse();
        snapshot.Reason.Should().Be(SystemExperienceReplayReasons.NoMatch);
    }

    private static ExperienceMatchDto CreateMatch(float similarity)
    {
        return new ExperienceMatchDto
        {
            Similarity = similarity,
            Experience = new SystemExperience
            {
                Id = Guid.NewGuid(),
                Type = ExperienceType.QA,
                Intent = "修复构建失败",
                SolutionSop = "运行构建。",
                ContextTags = ExperienceDistillationOutputProtocol.ContextTag
            }
        };
    }
}
