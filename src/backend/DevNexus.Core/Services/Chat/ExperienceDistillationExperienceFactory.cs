using DevNexus.Domain.Entities;
using DevNexus.Domain.Models;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 系统经验实体工厂。
/// </summary>
public static class ExperienceDistillationExperienceFactory
{
    /// <summary>
    /// 系统经验初始效用评分。
    /// </summary>
    public const double InitialUtilityScore = 5.0;

    /// <summary>
    /// 系统经验初始使用次数。
    /// </summary>
    public const int InitialUsageCount = 0;

    /// <summary>
    /// 根据提纯结果创建系统经验。
    /// </summary>
    public static SystemExperience CreateQaExperience(
        ExperienceDistillationParseResult parseResult,
        DateTime matchedAt)
    {
        return CreateQaExperience(parseResult, matchedAt, ExperienceDistillationScheduleContext.Empty);
    }

    /// <summary>
    /// 根据提纯结果和调度上下文创建系统经验。
    /// </summary>
    public static SystemExperience CreateQaExperience(
        ExperienceDistillationParseResult parseResult,
        DateTime matchedAt,
        ExperienceDistillationScheduleContext scheduleContext)
    {
        return CreateQaExperience(parseResult, matchedAt, scheduleContext, string.Empty);
    }

    /// <summary>
    /// 根据提纯结果、调度上下文和准入价值信号创建系统经验。
    /// </summary>
    public static SystemExperience CreateQaExperience(
        ExperienceDistillationParseResult parseResult,
        DateTime matchedAt,
        ExperienceDistillationScheduleContext scheduleContext,
        string matchedValueSignalKeyword)
    {
        return CreateQaExperience(
            parseResult,
            matchedAt,
            scheduleContext,
            matchedValueSignalKeyword,
            null);
    }

    /// <summary>
    /// 根据提纯结果、调度上下文、准入价值信号和来源会话创建系统经验。
    /// </summary>
    public static SystemExperience CreateQaExperience(
        ExperienceDistillationParseResult parseResult,
        DateTime matchedAt,
        ExperienceDistillationScheduleContext scheduleContext,
        string matchedValueSignalKeyword,
        Guid? sourceSessionId)
    {
        return CreateQaExperience(
            parseResult,
            matchedAt,
            scheduleContext,
            matchedValueSignalKeyword,
            sourceSessionId,
            string.Empty);
    }

    /// <summary>
    /// 根据提纯结果、调度上下文、准入价值信号、来源会话和 Prompt 指纹创建系统经验。
    /// </summary>
    public static SystemExperience CreateQaExperience(
        ExperienceDistillationParseResult parseResult,
        DateTime matchedAt,
        ExperienceDistillationScheduleContext scheduleContext,
        string matchedValueSignalKeyword,
        Guid? sourceSessionId,
        string distillationPromptFingerprint)
    {
        return new SystemExperience
        {
            Id = Guid.NewGuid(),
            Type = ExperienceType.QA,
            Intent = parseResult.Intent,
            SolutionSop = parseResult.SolutionSop,
            ContextTags = BuildContextTags(
                scheduleContext,
                matchedValueSignalKeyword,
                sourceSessionId,
                distillationPromptFingerprint),
            UtilityScore = InitialUtilityScore,
            UsageCount = InitialUsageCount,
            LastMatchedAt = matchedAt
        };
    }

    private static string BuildContextTags(
        ExperienceDistillationScheduleContext scheduleContext,
        string matchedValueSignalKeyword,
        Guid? sourceSessionId,
        string distillationPromptFingerprint)
    {
        var tags = new List<string> { ExperienceDistillationOutputProtocol.ContextTag };
        AppendTag(tags, ExperienceDistillationOutputProtocol.CandidateReasonTagPrefix, scheduleContext.CandidateReason);
        AppendTag(tags, ExperienceDistillationOutputProtocol.ContextPressureReasonTagPrefix, scheduleContext.ContextPressureReason);
        AppendTag(
            tags,
            ExperienceDistillationOutputProtocol.ContextCompressionFingerprintTagPrefix,
            scheduleContext.ContextCompressionSummaryFingerprint);
        AppendTag(
            tags,
            ExperienceDistillationOutputProtocol.DistillationPromptFingerprintTagPrefix,
            distillationPromptFingerprint);
        AppendTag(tags, ExperienceDistillationOutputProtocol.ValueSignalTagPrefix, matchedValueSignalKeyword);
        AppendTag(tags, ExperienceDistillationOutputProtocol.SourceSessionTagPrefix, sourceSessionId?.ToString("D") ?? string.Empty);

        return string.Join(",", tags.Distinct(StringComparer.Ordinal));
    }

    private static void AppendTag(List<string> tags, string prefix, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        tags.Add(prefix + value.Trim());
    }
}
