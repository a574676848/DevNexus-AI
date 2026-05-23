using DevNexus.Core.Services.Chat;
using DevNexus.Shared.Constants;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 系统经验回放效果评估测试。
/// </summary>
public sealed class SystemExperienceReplayEvaluationTests
{
    /// <summary>
    /// 可追踪的动态上下文回放应判定为有用召回。
    /// </summary>
    [Fact]
    public void Build_ShouldMarkUsefulRecall_WhenReplayIsTraceable()
    {
        var sourceSessionId = Guid.NewGuid();
        var snapshot = CreateSnapshot(
            SystemExperienceReplayReasons.DynamicContext,
            injectedDynamicContext: true,
            contextTags: BuildContextTags(sourceSessionId, includeValueSignal: true, includePromptFingerprint: true));

        var evaluation = SystemExperienceReplayEvaluation.Build(snapshot);

        evaluation.UsefulRecall.Should().BeTrue();
        evaluation.ContextPollutionRisk.Should().BeFalse();
        evaluation.UntraceableReuseRisk.Should().BeFalse();
        evaluation.HasCitationFingerprint.Should().BeTrue();
        evaluation.HasValueSignal.Should().BeTrue();
        evaluation.HasSourceSession.Should().BeTrue();
        evaluation.HasDistillationPromptFingerprint.Should().BeTrue();
        evaluation.EvaluationReason.Should().Be(SystemExperienceReplayEvaluationReasons.TraceableUsefulRecall);
    }

    /// <summary>
    /// 动态上下文缺少价值信号时应标记污染风险。
    /// </summary>
    [Fact]
    public void Build_ShouldMarkContextPollutionRisk_WhenDynamicContextMissingValueSignal()
    {
        var sourceSessionId = Guid.NewGuid();
        var snapshot = CreateSnapshot(
            SystemExperienceReplayReasons.DynamicContext,
            injectedDynamicContext: true,
            contextTags: BuildContextTags(sourceSessionId, includeValueSignal: false, includePromptFingerprint: true));

        var evaluation = SystemExperienceReplayEvaluation.Build(snapshot);

        evaluation.UsefulRecall.Should().BeFalse();
        evaluation.ContextPollutionRisk.Should().BeTrue();
        evaluation.UntraceableReuseRisk.Should().BeFalse();
        evaluation.HasValueSignal.Should().BeFalse();
        evaluation.EvaluationReason.Should().Be(SystemExperienceReplayEvaluationReasons.DynamicContextMissingValueSignal);
    }

    /// <summary>
    /// 直接命中缺少来源事实时应标记不可追踪复用风险。
    /// </summary>
    [Fact]
    public void Build_ShouldMarkUntraceableReuseRisk_WhenDirectAnswerMissingCitationFacts()
    {
        var snapshot = CreateSnapshot(
            SystemExperienceReplayReasons.DirectAnswer,
            answeredDirectly: true,
            contextTags: ExperienceDistillationOutputProtocol.ContextTag);

        var evaluation = SystemExperienceReplayEvaluation.Build(snapshot);

        evaluation.UsefulRecall.Should().BeFalse();
        evaluation.ContextPollutionRisk.Should().BeFalse();
        evaluation.UntraceableReuseRisk.Should().BeTrue();
        evaluation.EvaluationReason.Should().Be(SystemExperienceReplayEvaluationReasons.MissingCitationFacts);
    }

    /// <summary>
    /// 即使引用事实完整，相似度低于有效召回阈值时也不应判定为有用召回。
    /// </summary>
    [Fact]
    public void Build_ShouldMarkBelowUsefulThreshold_WhenSimilarityIsTooLow()
    {
        var sourceSessionId = Guid.NewGuid();
        var snapshot = CreateSnapshot(
            SystemExperienceReplayReasons.DynamicContext,
            injectedDynamicContext: true,
            similarity: MemoryConstants.ChatPartialHitThreshold - 0.01f,
            contextTags: BuildContextTags(sourceSessionId, includeValueSignal: true, includePromptFingerprint: true));

        var evaluation = SystemExperienceReplayEvaluation.Build(snapshot);

        evaluation.UsefulRecall.Should().BeFalse();
        evaluation.ContextPollutionRisk.Should().BeFalse();
        evaluation.UntraceableReuseRisk.Should().BeFalse();
        evaluation.HasCitationFingerprint.Should().BeTrue();
        evaluation.HasValueSignal.Should().BeTrue();
        evaluation.HasSourceSession.Should().BeTrue();
        evaluation.HasDistillationPromptFingerprint.Should().BeTrue();
        evaluation.EvaluationReason.Should().Be(SystemExperienceReplayEvaluationReasons.BelowUsefulThreshold);
    }

    /// <summary>
    /// 未复用系统经验时应保持空评估。
    /// </summary>
    [Fact]
    public void Build_ShouldReturnEmptyEvaluation_WhenNotReplayed()
    {
        var evaluation = SystemExperienceReplayEvaluation.Build(SystemExperienceReplaySnapshot.Empty);

        evaluation.WasReplayed.Should().BeFalse();
        evaluation.UsefulRecall.Should().BeFalse();
        evaluation.ContextPollutionRisk.Should().BeFalse();
        evaluation.UntraceableReuseRisk.Should().BeFalse();
        evaluation.EvaluationReason.Should().Be(SystemExperienceReplayEvaluationReasons.NotReplayed);
    }

    private static SystemExperienceReplaySnapshot CreateSnapshot(
        string reason,
        string contextTags,
        bool answeredDirectly = false,
        bool injectedDynamicContext = false,
        float? similarity = null)
    {
        return new SystemExperienceReplaySnapshot
        {
            HasMatch = true,
            AnsweredDirectly = answeredDirectly,
            InjectedDynamicContext = injectedDynamicContext,
            Reason = reason,
            ExperienceId = Guid.NewGuid(),
            Similarity = similarity ?? MemoryConstants.ChatPartialHitThreshold,
            ContextTags = contextTags,
            ContextTagSnapshot = SystemExperienceContextTagSnapshot.Parse(contextTags)
        };
    }

    private static string BuildContextTags(
        Guid sourceSessionId,
        bool includeValueSignal,
        bool includePromptFingerprint)
    {
        var tags = new List<string>
        {
            ExperienceDistillationOutputProtocol.ContextTag,
            ExperienceDistillationOutputProtocol.SourceSessionTagPrefix + sourceSessionId.ToString("D")
        };

        if (includeValueSignal)
        {
            tags.Add(ExperienceDistillationOutputProtocol.ValueSignalTagPrefix + "流程");
        }

        if (includePromptFingerprint)
        {
            tags.Add(ExperienceDistillationOutputProtocol.DistillationPromptFingerprintTagPrefix + "prompt-fingerprint");
        }

        return string.Join(",", tags);
    }
}
