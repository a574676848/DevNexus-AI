using DevNexus.Core.Services.Chat;
using DevNexus.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 自我迭代复盘策略测试。
/// </summary>
public sealed class SelfIterationReviewPolicyTests
{
    /// <summary>
    /// 新经验已索引时应标记为形成长期经验。
    /// </summary>
    [Fact]
    public void Decide_ShouldMarkNewExperience_WhenCreatedAndIndexed()
    {
        var saveResult = SystemExperienceSaveResultFactory.CreatedAndIndexed(new SystemExperience());

        var decision = SelfIterationReviewPolicy.Decide(saveResult);

        decision.HasNewExperience.Should().BeTrue();
        decision.ShouldObserveOnly.Should().BeFalse();
        decision.RequiresRepairAttention.Should().BeFalse();
        decision.Reason.Should().Be(SelfIterationReviewReasons.ExperienceCreatedAndIndexed);
        decision.MemoryCitation.CitationFingerprint.Should().Be(saveResult.CitationFingerprint);
        decision.AttemptMemoryCitation.CitationFingerprint.Should().Be(saveResult.AttemptCitationFingerprint);
    }

    /// <summary>
    /// 重复经验应只观察，不再扩大自我迭代动作。
    /// </summary>
    [Fact]
    public void Decide_ShouldObserveOnly_WhenDuplicateSkipped()
    {
        var saveResult = SystemExperienceSaveResultFactory.Duplicate(new SystemExperience());

        var decision = SelfIterationReviewPolicy.Decide(saveResult);

        decision.HasNewExperience.Should().BeFalse();
        decision.ShouldObserveOnly.Should().BeTrue();
        decision.RequiresRepairAttention.Should().BeFalse();
        decision.Reason.Should().Be(SelfIterationReviewReasons.ExperienceDuplicateSkipped);
        decision.MemoryCitation.CitationFingerprint.Should().Be(saveResult.CitationFingerprint);
        decision.AttemptMemoryCitation.CitationFingerprint.Should().Be(saveResult.AttemptCitationFingerprint);
    }

    /// <summary>
    /// 新经验索引失败时应要求修复关注。
    /// </summary>
    [Fact]
    public void Decide_ShouldRequireRepairAttention_WhenIndexFailed()
    {
        var saveResult = SystemExperienceSaveResultFactory.CreatedButIndexFailed(new SystemExperience());

        var decision = SelfIterationReviewPolicy.Decide(saveResult);

        decision.HasNewExperience.Should().BeTrue();
        decision.ShouldObserveOnly.Should().BeFalse();
        decision.RequiresRepairAttention.Should().BeTrue();
        decision.Reason.Should().Be(SelfIterationReviewReasons.ExperienceCreatedButIndexFailed);
        decision.MemoryCitation.CitationFingerprint.Should().Be(saveResult.CitationFingerprint);
        decision.AttemptMemoryCitation.CitationFingerprint.Should().Be(saveResult.AttemptCitationFingerprint);
    }

    /// <summary>
    /// 提纯被跳过时应形成观察型复盘。
    /// </summary>
    [Fact]
    public void ObserveSkippedDistillation_ShouldReturnObserveOnlyDecision()
    {
        var decision = SelfIterationReviewPolicy.ObserveSkippedDistillation(
            SelfIterationReviewReasons.PreconditionSkipped);

        decision.HasNewExperience.Should().BeFalse();
        decision.ShouldObserveOnly.Should().BeTrue();
        decision.RequiresRepairAttention.Should().BeFalse();
        decision.Reason.Should().Be(SelfIterationReviewReasons.PreconditionSkipped);
    }

    /// <summary>
    /// 前置条件跳过原因应统一归类为前置条件复盘。
    /// </summary>
    [Theory]
    [InlineData(SelfIterationSkipReasons.TooFewMessages)]
    [InlineData(SelfIterationSkipReasons.SwarmSession)]
    [InlineData(SelfIterationSkipReasons.MissingQaPair)]
    [InlineData(SelfIterationSkipReasons.ProviderMissing)]
    public void ObserveSkippedDistillationBySkipReason_ShouldClassifyPreconditions(string skipReason)
    {
        var decision = SelfIterationReviewPolicy.ObserveSkippedDistillationBySkipReason(skipReason);

        decision.ShouldObserveOnly.Should().BeTrue();
        decision.Reason.Should().Be(SelfIterationReviewReasons.PreconditionSkipped);
    }

    /// <summary>
    /// 模型调用跳过原因应统一归类为模型调用复盘。
    /// </summary>
    [Theory]
    [InlineData(SelfIterationSkipReasons.ModelTimeout)]
    [InlineData(SelfIterationSkipReasons.ModelCancelled)]
    [InlineData(SelfIterationSkipReasons.ModelInterrupted)]
    public void ObserveSkippedDistillationBySkipReason_ShouldClassifyModelInvocation(string skipReason)
    {
        var decision = SelfIterationReviewPolicy.ObserveSkippedDistillationBySkipReason(skipReason);

        decision.ShouldObserveOnly.Should().BeTrue();
        decision.Reason.Should().Be(SelfIterationReviewReasons.ModelInvocationSkipped);
    }

    /// <summary>
    /// 准入拒绝原因应统一归类为准入复盘。
    /// </summary>
    [Theory]
    [InlineData(ExperienceDistillationAdmissionReasons.MissingQaPair)]
    [InlineData(ExperienceDistillationAdmissionReasons.ContentTooShort)]
    [InlineData(ExperienceDistillationAdmissionReasons.SkipConditionMatched)]
    [InlineData(ExperienceDistillationAdmissionReasons.MissingValueSignal)]
    public void ObserveSkippedDistillationBySkipReason_ShouldClassifyAdmission(string skipReason)
    {
        var decision = SelfIterationReviewPolicy.ObserveSkippedDistillationBySkipReason(skipReason);

        decision.ShouldObserveOnly.Should().BeTrue();
        decision.Reason.Should().Be(SelfIterationReviewReasons.AdmissionSkipped);
    }

    /// <summary>
    /// 解析拒绝原因应统一归类为解析复盘。
    /// </summary>
    [Theory]
    [InlineData(ExperienceDistillationParseReasons.Empty)]
    [InlineData(ExperienceDistillationParseReasons.NoValue)]
    [InlineData(ExperienceDistillationParseReasons.MissingIntent)]
    [InlineData(ExperienceDistillationParseReasons.MissingIntentMarker)]
    [InlineData(ExperienceDistillationParseReasons.MissingSop)]
    [InlineData(ExperienceDistillationParseReasons.MarkdownCodeBlock)]
    [InlineData(ExperienceDistillationParseReasons.NoValueWithContent)]
    [InlineData(ExperienceDistillationParseReasons.SopTooLong)]
    [InlineData(ExperienceDistillationParseReasons.RawTranscriptLeak)]
    public void ObserveSkippedDistillationBySkipReason_ShouldClassifyParse(string skipReason)
    {
        var decision = SelfIterationReviewPolicy.ObserveSkippedDistillationBySkipReason(skipReason);

        decision.ShouldObserveOnly.Should().BeTrue();
        decision.Reason.Should().Be(SelfIterationReviewReasons.ParseSkipped);
    }

    /// <summary>
    /// 解析跳过复盘应可携带未落盘引用事实。
    /// </summary>
    [Fact]
    public void ObserveSkippedDistillationBySkipReason_ShouldCarryCitation_WhenCitationExists()
    {
        var sourceSessionId = Guid.NewGuid();
        var citation = SystemExperienceMemoryCitation.CreateUnpersistedDistillationCitation(
            sourceSessionId,
            "流程",
            "prompt-fingerprint");

        var decision = SelfIterationReviewPolicy.ObserveSkippedDistillationBySkipReason(
            ExperienceDistillationParseReasons.RawTranscriptLeak,
            citation);

        decision.ShouldObserveOnly.Should().BeTrue();
        decision.Reason.Should().Be(SelfIterationReviewReasons.ParseSkipped);
        decision.MemoryCitation.ExperienceId.Should().BeNull();
        decision.MemoryCitation.SourceSessionId.Should().Be(sourceSessionId);
        decision.MemoryCitation.ValueSignalKeyword.Should().Be("流程");
        decision.MemoryCitation.DistillationPromptFingerprint.Should().Be("prompt-fingerprint");
        decision.MemoryCitation.CitationFingerprint.Should().Be(citation.CitationFingerprint);
    }

    /// <summary>
    /// 未识别跳过原因应保持观察，不扩大动作。
    /// </summary>
    [Fact]
    public void ObserveSkippedDistillationBySkipReason_ShouldClassifyUnknownAsUnclassified()
    {
        var decision = SelfIterationReviewPolicy.ObserveSkippedDistillationBySkipReason("unknown");

        decision.ShouldObserveOnly.Should().BeTrue();
        decision.Reason.Should().Be(SelfIterationReviewReasons.SaveResultUnclassified);
    }

    /// <summary>
    /// 提纯跳过细分原因应由 Core 统一定义。
    /// </summary>
    [Fact]
    public void SkipReasons_ShouldExposeStableDistillationReasons()
    {
        SelfIterationSkipReasons.TooFewMessages.Should().Be("too-few-messages");
        SelfIterationSkipReasons.SwarmSession.Should().Be("swarm-session");
        SelfIterationSkipReasons.MissingQaPair.Should().Be("missing-qa-pair");
        SelfIterationSkipReasons.ProviderMissing.Should().Be("provider-missing");
        SelfIterationSkipReasons.ModelTimeout.Should().Be("model-timeout");
        SelfIterationSkipReasons.ModelCancelled.Should().Be("model-cancelled");
        SelfIterationSkipReasons.ModelInterrupted.Should().Be("model-interrupted");
    }
}
