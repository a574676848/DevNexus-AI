namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 自我迭代复盘原因。
/// </summary>
public static class SelfIterationReviewReasons
{
    /// <summary>
    /// 新经验已完成持久化和索引。
    /// </summary>
    public const string ExperienceCreatedAndIndexed = "experience-created-and-indexed";

    /// <summary>
    /// 新经验已持久化但索引失败。
    /// </summary>
    public const string ExperienceCreatedButIndexFailed = "experience-created-but-index-failed";

    /// <summary>
    /// 候选经验与已有经验重复。
    /// </summary>
    public const string ExperienceDuplicateSkipped = "experience-duplicate-skipped";

    /// <summary>
    /// 经验提纯准入阶段已跳过。
    /// </summary>
    public const string AdmissionSkipped = "admission-skipped";

    /// <summary>
    /// 经验提纯前置条件不足。
    /// </summary>
    public const string PreconditionSkipped = "precondition-skipped";

    /// <summary>
    /// 经验提纯模型调用已跳过。
    /// </summary>
    public const string ModelInvocationSkipped = "model-invocation-skipped";

    /// <summary>
    /// 经验提纯输出解析阶段已跳过。
    /// </summary>
    public const string ParseSkipped = "parse-skipped";

    /// <summary>
    /// 保存结果缺少可识别信号。
    /// </summary>
    public const string SaveResultUnclassified = "save-result-unclassified";
}
