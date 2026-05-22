namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 系统经验回放决策原因。
/// </summary>
public static class SystemExperienceReplayReasons
{
    /// <summary>
    /// 未命中系统经验。
    /// </summary>
    public const string NoMatch = "no-match";

    /// <summary>
    /// 命中不足以回放。
    /// </summary>
    public const string BelowReplayThreshold = "below-replay-threshold";

    /// <summary>
    /// 可直接使用系统经验答案。
    /// </summary>
    public const string DirectAnswer = "direct-answer";

    /// <summary>
    /// 可作为动态上下文参考。
    /// </summary>
    public const string DynamicContext = "dynamic-context";
}
