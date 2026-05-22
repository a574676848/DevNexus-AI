namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 系统经验记忆引用事实。
/// </summary>
public sealed class SystemExperienceMemoryCitation
{
    /// <summary>
    /// 空引用事实。
    /// </summary>
    public static readonly SystemExperienceMemoryCitation Empty = new();

    /// <summary>
    /// 经验标识。
    /// </summary>
    public Guid? ExperienceId { get; init; }

    /// <summary>
    /// 来源会话标识。
    /// </summary>
    public Guid? SourceSessionId { get; init; }

    /// <summary>
    /// 触发经验沉淀的价值信号。
    /// </summary>
    public string ValueSignalKeyword { get; init; } = string.Empty;

    /// <summary>
    /// 提纯协议版本。
    /// </summary>
    public string DistillationProtocol { get; init; } = string.Empty;

    /// <summary>
    /// 经验提纯 Prompt 指纹。
    /// </summary>
    public string DistillationPromptFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// 引用事实稳定指纹。
    /// </summary>
    public string CitationFingerprint => PromptFingerprint.ComputeHash(string.Join(
        "\n",
        FormatGuid(ExperienceId),
        FormatGuid(SourceSessionId),
        FormatValue(ValueSignalKeyword),
        FormatValue(DistillationProtocol),
        FormatValue(DistillationPromptFingerprint)));

    /// <summary>
    /// 从经验标识和上下文标签快照创建引用事实。
    /// </summary>
    public static SystemExperienceMemoryCitation FromContextTags(Guid? experienceId, string? contextTags)
    {
        return FromTagSnapshot(experienceId, SystemExperienceContextTagSnapshot.Parse(contextTags));
    }

    /// <summary>
    /// 从经验标识和上下文标签快照创建引用事实。
    /// </summary>
    public static SystemExperienceMemoryCitation FromTagSnapshot(
        Guid? experienceId,
        SystemExperienceContextTagSnapshot tagSnapshot)
    {
        return new SystemExperienceMemoryCitation
        {
            ExperienceId = experienceId,
            SourceSessionId = tagSnapshot.SourceSessionId,
            ValueSignalKeyword = tagSnapshot.ValueSignalKeyword,
            DistillationProtocol = tagSnapshot.DistillationProtocol,
            DistillationPromptFingerprint = tagSnapshot.DistillationPromptFingerprint
        };
    }

    /// <summary>
    /// 创建准入成功但未落盘经验的引用事实。
    /// </summary>
    public static SystemExperienceMemoryCitation CreateUnpersistedDistillationCitation(
        Guid sourceSessionId,
        string valueSignalKeyword,
        string distillationPromptFingerprint)
    {
        return new SystemExperienceMemoryCitation
        {
            SourceSessionId = sourceSessionId,
            ValueSignalKeyword = valueSignalKeyword,
            DistillationProtocol = ExperienceDistillationOutputProtocol.Version,
            DistillationPromptFingerprint = distillationPromptFingerprint
        };
    }

    /// <summary>
    /// 渲染为动态上下文中的轻量引用片段。
    /// </summary>
    public string ToPromptBlock()
    {
        return $"""
### MemoryCitation
- ExperienceId: {FormatGuid(ExperienceId)}
- SourceSessionId: {FormatGuid(SourceSessionId)}
- ValueSignal: {FormatValue(ValueSignalKeyword)}
- DistillationProtocol: {FormatValue(DistillationProtocol)}
- DistillationPromptFingerprint: {FormatValue(DistillationPromptFingerprint)}
- CitationFingerprint: {CitationFingerprint}
""";
    }

    private static string FormatGuid(Guid? value)
    {
        return value?.ToString("D") ?? "none";
    }

    private static string FormatValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "none" : value.Trim();
    }
}
