using DevNexus.Shared.Constants;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 系统经验回放评估原因。
/// </summary>
public static class SystemExperienceReplayEvaluationReasons
{
    /// <summary>
    /// 未复用系统经验。
    /// </summary>
    public const string NotReplayed = "not-replayed";

    /// <summary>
    /// 已复用系统经验且引用事实可追踪。
    /// </summary>
    public const string TraceableUsefulRecall = "traceable-useful-recall";

    /// <summary>
    /// 已复用系统经验但引用事实不完整。
    /// </summary>
    public const string MissingCitationFacts = "missing-citation-facts";

    /// <summary>
    /// 动态上下文缺少长期价值信号。
    /// </summary>
    public const string DynamicContextMissingValueSignal = "dynamic-context-missing-value-signal";

    /// <summary>
    /// 已复用系统经验但相似度不足以证明有效召回。
    /// </summary>
    public const string BelowUsefulThreshold = "below-useful-threshold";
}

/// <summary>
/// 系统经验回放效果评估快照。
/// </summary>
public sealed record SystemExperienceReplayEvaluationSnapshot
{
    /// <summary>
    /// 是否命中系统经验。
    /// </summary>
    public bool HasMatch { get; init; }

    /// <summary>
    /// 是否实际复用系统经验。
    /// </summary>
    public bool WasReplayed { get; init; }

    /// <summary>
    /// 是否形成有用召回。
    /// </summary>
    public bool UsefulRecall { get; init; }

    /// <summary>
    /// 是否存在动态上下文污染风险。
    /// </summary>
    public bool ContextPollutionRisk { get; init; }

    /// <summary>
    /// 是否存在不可追踪复用风险。
    /// </summary>
    public bool UntraceableReuseRisk { get; init; }

    /// <summary>
    /// 回放原因。
    /// </summary>
    public string ReplayReason { get; init; } = SystemExperienceReplayReasons.NoMatch;

    /// <summary>
    /// 评估原因。
    /// </summary>
    public string EvaluationReason { get; init; } = SystemExperienceReplayEvaluationReasons.NotReplayed;

    /// <summary>
    /// 匹配相似度。
    /// </summary>
    public float? Similarity { get; init; }

    /// <summary>
    /// 是否具备稳定引用指纹。
    /// </summary>
    public bool HasCitationFingerprint { get; init; }

    /// <summary>
    /// 是否具备长期价值信号。
    /// </summary>
    public bool HasValueSignal { get; init; }

    /// <summary>
    /// 是否具备来源会话。
    /// </summary>
    public bool HasSourceSession { get; init; }

    /// <summary>
    /// 是否具备提纯 Prompt 指纹。
    /// </summary>
    public bool HasDistillationPromptFingerprint { get; init; }
}

/// <summary>
/// 系统经验回放效果评估器。
/// </summary>
public static class SystemExperienceReplayEvaluation
{
    /// <summary>
    /// 根据系统经验回放快照构建效果评估快照。
    /// </summary>
    public static SystemExperienceReplayEvaluationSnapshot Build(SystemExperienceReplaySnapshot? replay)
    {
        if (replay == null || !replay.WasReplayed)
        {
            return new SystemExperienceReplayEvaluationSnapshot();
        }

        var citation = replay.MemoryCitation;
        var hasCitationFingerprint = !string.IsNullOrWhiteSpace(citation.CitationFingerprint);
        var hasValueSignal = !string.IsNullOrWhiteSpace(citation.ValueSignalKeyword);
        var hasSourceSession = citation.SourceSessionId.HasValue;
        var hasPromptFingerprint = !string.IsNullOrWhiteSpace(citation.DistillationPromptFingerprint);
        var traceable = hasCitationFingerprint && hasSourceSession && hasPromptFingerprint;
        var useful = traceable
            && hasValueSignal
            && replay.Similarity >= MemoryConstants.ChatPartialHitThreshold;
        var dynamicContextMissingValueSignal = replay.InjectedDynamicContext && !hasValueSignal;

        return new SystemExperienceReplayEvaluationSnapshot
        {
            HasMatch = replay.HasMatch,
            WasReplayed = replay.WasReplayed,
            UsefulRecall = useful,
            ContextPollutionRisk = replay.InjectedDynamicContext && (!traceable || !hasValueSignal),
            UntraceableReuseRisk = replay.WasReplayed && !traceable,
            ReplayReason = replay.Reason,
            EvaluationReason = ResolveReason(useful, traceable, dynamicContextMissingValueSignal),
            Similarity = replay.Similarity,
            HasCitationFingerprint = hasCitationFingerprint,
            HasValueSignal = hasValueSignal,
            HasSourceSession = hasSourceSession,
            HasDistillationPromptFingerprint = hasPromptFingerprint
        };
    }

    private static string ResolveReason(
        bool useful,
        bool traceable,
        bool dynamicContextMissingValueSignal)
    {
        if (useful)
        {
            return SystemExperienceReplayEvaluationReasons.TraceableUsefulRecall;
        }

        if (dynamicContextMissingValueSignal)
        {
            return SystemExperienceReplayEvaluationReasons.DynamicContextMissingValueSignal;
        }

        return traceable
            ? SystemExperienceReplayEvaluationReasons.BelowUsefulThreshold
            : SystemExperienceReplayEvaluationReasons.MissingCitationFacts;
    }
}
