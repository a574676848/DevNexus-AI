namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 自我迭代跳过原因。
/// </summary>
public static class SelfIterationSkipReasons
{
    /// <summary>
    /// 会话消息不足。
    /// </summary>
    public const string TooFewMessages = "too-few-messages";

    /// <summary>
    /// Swarm 会话不走普通 QA 提纯。
    /// </summary>
    public const string SwarmSession = "swarm-session";

    /// <summary>
    /// 缺少可提纯 QA 对。
    /// </summary>
    public const string MissingQaPair = "missing-qa-pair";

    /// <summary>
    /// 缺少默认 Provider。
    /// </summary>
    public const string ProviderMissing = "provider-missing";

    /// <summary>
    /// 模型调用超时。
    /// </summary>
    public const string ModelTimeout = "model-timeout";

    /// <summary>
    /// 模型调用被取消。
    /// </summary>
    public const string ModelCancelled = "model-cancelled";

    /// <summary>
    /// 模型调用被中断。
    /// </summary>
    public const string ModelInterrupted = "model-interrupted";
}
