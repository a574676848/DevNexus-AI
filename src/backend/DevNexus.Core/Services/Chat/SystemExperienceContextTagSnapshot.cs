namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 系统经验上下文标签快照。
/// </summary>
public sealed class SystemExperienceContextTagSnapshot
{
    /// <summary>
    /// 空标签快照。
    /// </summary>
    public static readonly SystemExperienceContextTagSnapshot Empty = new();

    /// <summary>
    /// 是否包含提纯协议标签。
    /// </summary>
    public bool HasDistillationProtocol { get; init; }

    /// <summary>
    /// 提纯协议版本。
    /// </summary>
    public string DistillationProtocol { get; init; } = string.Empty;

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
    /// 经验提纯 Prompt 指纹。
    /// </summary>
    public string DistillationPromptFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// 触发提纯准入的长期价值信号关键词。
    /// </summary>
    public string ValueSignalKeyword { get; init; } = string.Empty;

    /// <summary>
    /// 经验来源会话标识。
    /// </summary>
    public Guid? SourceSessionId { get; init; }

    /// <summary>
    /// 经验语义指纹。
    /// </summary>
    public string SemanticFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// 是否包含自我迭代调度事实。
    /// </summary>
    public bool HasSelfIterationFacts =>
        !string.IsNullOrWhiteSpace(CandidateReason)
        || !string.IsNullOrWhiteSpace(ContextPressureReason)
        || !string.IsNullOrWhiteSpace(ContextCompressionSummaryFingerprint)
        || !string.IsNullOrWhiteSpace(DistillationPromptFingerprint);

    /// <summary>
    /// 从逗号分隔标签解析结构化快照。
    /// </summary>
    public static SystemExperienceContextTagSnapshot Parse(string? contextTags)
    {
        var tags = SplitTags(contextTags).ToList();
        if (tags.Count == 0)
        {
            return Empty;
        }

        return new SystemExperienceContextTagSnapshot
        {
            HasDistillationProtocol = TryGetSuffix(
                tags,
                ExperienceDistillationOutputProtocol.ContextTagPrefix,
                out var protocol),
            DistillationProtocol = protocol,
            CandidateReason = ReadSuffix(tags, ExperienceDistillationOutputProtocol.CandidateReasonTagPrefix),
            ContextPressureReason = ReadSuffix(tags, ExperienceDistillationOutputProtocol.ContextPressureReasonTagPrefix),
            ContextCompressionSummaryFingerprint = ReadSuffix(
                tags,
                ExperienceDistillationOutputProtocol.ContextCompressionFingerprintTagPrefix),
            DistillationPromptFingerprint = ReadSuffix(
                tags,
                ExperienceDistillationOutputProtocol.DistillationPromptFingerprintTagPrefix),
            ValueSignalKeyword = ReadSuffix(tags, ExperienceDistillationOutputProtocol.ValueSignalTagPrefix),
            SourceSessionId = ReadGuidSuffix(tags, ExperienceDistillationOutputProtocol.SourceSessionTagPrefix),
            SemanticFingerprint = ReadSuffix(tags, SystemExperienceFingerprint.ContextTagPrefix)
        };
    }

    private static Guid? ReadGuidSuffix(IReadOnlyList<string> tags, string prefix)
    {
        return TryGetSuffix(tags, prefix, out var value) && Guid.TryParse(value, out var id)
            ? id
            : null;
    }

    private static string ReadSuffix(IReadOnlyList<string> tags, string prefix)
    {
        return TryGetSuffix(tags, prefix, out var value) ? value : string.Empty;
    }

    private static bool TryGetSuffix(IReadOnlyList<string> tags, string prefix, out string value)
    {
        var tag = tags.FirstOrDefault(item => item.StartsWith(prefix, StringComparison.Ordinal));
        value = tag == null ? string.Empty : tag[prefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static IEnumerable<string> SplitTags(string? contextTags)
    {
        return (contextTags ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => !string.IsNullOrWhiteSpace(tag));
    }
}
