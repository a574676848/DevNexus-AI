namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 系统经验提纯解析结果。
/// </summary>
public sealed class ExperienceDistillationParseResult
{
    /// <summary>
    /// 是否具有提纯价值。
    /// </summary>
    public bool HasValue { get; init; }

    /// <summary>
    /// 意图。
    /// </summary>
    public string Intent { get; init; } = string.Empty;

    /// <summary>
    /// SOP 内容。
    /// </summary>
    public string SolutionSop { get; init; } = string.Empty;

    /// <summary>
    /// 解析失败或无价值原因。
    /// </summary>
    public string Reason { get; init; } = ExperienceDistillationParseReasons.Empty;
}
