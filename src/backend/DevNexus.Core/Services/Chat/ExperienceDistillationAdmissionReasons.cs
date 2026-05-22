namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 系统经验提纯准入原因。
/// </summary>
public static class ExperienceDistillationAdmissionReasons
{
    /// <summary>
    /// 缺少 QA 对。
    /// </summary>
    public const string MissingQaPair = "admission-missing-qa-pair";

    /// <summary>
    /// 内容过短。
    /// </summary>
    public const string ContentTooShort = "content-too-short";

    /// <summary>
    /// 缺少长期经验价值信号。
    /// </summary>
    public const string MissingValueSignal = "missing-value-signal";

    /// <summary>
    /// 命中不应提纯的跳过条件。
    /// </summary>
    public const string SkipConditionMatched = "skip-condition-matched";

    /// <summary>
    /// 已准入。
    /// </summary>
    public const string Accepted = "accepted";
}
