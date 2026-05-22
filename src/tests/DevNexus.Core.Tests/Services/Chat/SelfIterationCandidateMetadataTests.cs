using DevNexus.Core.Services.Chat;
using DevNexus.Shared.Constants;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 自我迭代候选 metadata 协议测试。
/// </summary>
public sealed class SelfIterationCandidateMetadataTests
{
    /// <summary>
    /// 候选决策应写入可持久化消息 metadata。
    /// </summary>
    [Fact]
    public void Apply_ShouldPersistCandidateFacts()
    {
        var metadata = new Dictionary<string, object>();
        var decision = new SelfIterationCandidateDecision
        {
            ShouldDistillExperience = true,
            Reason = SelfIterationCandidateReasons.SummaryCompressionResolved,
            ContextPressureReason = ChatHistoryPressureReasons.SummaryCompression,
            ContextCompressionSummaryFingerprint = "summary-fingerprint"
        };

        SelfIterationCandidateMetadata.Apply(metadata, decision);

        metadata[ChatMessageMetadataKeys.SelfIterationShouldDistill].Should().Be(true);
        metadata[ChatMessageMetadataKeys.SelfIterationObserveOnly].Should().Be(false);
        metadata[ChatMessageMetadataKeys.SelfIterationCandidateReason]
            .Should().Be(SelfIterationCandidateReasons.SummaryCompressionResolved);
        metadata[ChatMessageMetadataKeys.SelfIterationContextPressureReason]
            .Should().Be(ChatHistoryPressureReasons.SummaryCompression);
        metadata[ChatMessageMetadataKeys.SelfIterationContextCompressionSummaryFingerprint]
            .Should().Be("summary-fingerprint");
    }

    /// <summary>
    /// 复用系统经验时应只持久化低噪引用指纹。
    /// </summary>
    [Fact]
    public void Apply_ShouldPersistReusedExperienceCitationFingerprint()
    {
        var sourceSessionId = Guid.NewGuid();
        var metadata = new Dictionary<string, object>();
        var citation = new SystemExperienceMemoryCitation
        {
            SourceSessionId = sourceSessionId,
            ValueSignalKeyword = "流程",
            DistillationProtocol = ExperienceDistillationOutputProtocol.Version,
            DistillationPromptFingerprint = "prompt-fingerprint"
        };
        var decision = new SelfIterationCandidateDecision
        {
            ShouldObserveOnly = true,
            Reason = SelfIterationCandidateReasons.SystemExperienceReused,
            ReusedExperienceMemoryCitation = citation
        };

        SelfIterationCandidateMetadata.Apply(metadata, decision);

        metadata[ChatMessageMetadataKeys.ReusedExperienceCitationFingerprint]
            .Should().Be(citation.CitationFingerprint);
        metadata.Keys.Should().NotContain("reusedExperienceValueSignal");
        metadata.Keys.Should().NotContain("reusedExperienceSourceSessionId");
        metadata.Keys.Should().NotContain("reusedExperienceDistillationPromptFingerprint");
    }
}
