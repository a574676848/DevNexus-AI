namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 系统经验提纯解析原因。
/// </summary>
public static class ExperienceDistillationParseReasons
{
    /// <summary>
    /// 空输出。
    /// </summary>
    public const string Empty = "empty";

    /// <summary>
    /// 模型判断无提纯价值。
    /// </summary>
    public const string NoValue = "no-value";

    /// <summary>
    /// 缺少意图。
    /// </summary>
    public const string MissingIntent = "missing-intent";

    /// <summary>
    /// 缺少意图标记。
    /// </summary>
    public const string MissingIntentMarker = "missing-intent-marker";

    /// <summary>
    /// 缺少 SOP。
    /// </summary>
    public const string MissingSop = "missing-sop";

    /// <summary>
    /// 输出混入 Markdown 代码块。
    /// </summary>
    public const string MarkdownCodeBlock = "markdown-code-block";

    /// <summary>
    /// 无价值标记后仍混入正文。
    /// </summary>
    public const string NoValueWithContent = "no-value-with-content";

    /// <summary>
    /// SOP 超过可持久化长度上限。
    /// </summary>
    public const string SopTooLong = "sop-too-long";

    /// <summary>
    /// SOP 混入原始 QA、日志或工具输出。
    /// </summary>
    public const string RawTranscriptLeak = "raw-transcript-leak";

    /// <summary>
    /// 已提取有效经验。
    /// </summary>
    public const string ValueExtracted = "value-extracted";
}
