namespace DevNexus.Domain.Models;

/// <summary>
/// 经验提纯调度上下文。
/// </summary>
public sealed class ExperienceDistillationScheduleContext
{
    /// <summary>
    /// 空调度上下文。
    /// </summary>
    public static ExperienceDistillationScheduleContext Empty { get; } = new();

    /// <summary>
    /// 自我迭代候选原因。
    /// </summary>
    public string CandidateReason { get; init; } = string.Empty;

    /// <summary>
    /// 上下文压力原因。
    /// </summary>
    public string ContextPressureReason { get; init; } = string.Empty;

    /// <summary>
    /// 上下文压缩摘要指纹。
    /// </summary>
    public string ContextCompressionSummaryFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// 是否包含任一调度事实。
    /// </summary>
    public bool HasFacts =>
        !string.IsNullOrWhiteSpace(CandidateReason)
        || !string.IsNullOrWhiteSpace(ContextPressureReason)
        || !string.IsNullOrWhiteSpace(ContextCompressionSummaryFingerprint);

    /// <summary>
    /// 根据输入创建规范化调度上下文。
    /// </summary>
    public static ExperienceDistillationScheduleContext Create(
        string? candidateReason,
        string? contextPressureReason,
        string? contextCompressionSummaryFingerprint)
    {
        return new ExperienceDistillationScheduleContext
        {
            CandidateReason = candidateReason?.Trim() ?? string.Empty,
            ContextPressureReason = contextPressureReason?.Trim() ?? string.Empty,
            ContextCompressionSummaryFingerprint = contextCompressionSummaryFingerprint?.Trim() ?? string.Empty
        };
    }
}
