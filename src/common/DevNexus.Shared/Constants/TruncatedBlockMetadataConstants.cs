namespace DevNexus.Shared.Constants;

/// <summary>
/// Truncated Block metadata 的共享协议定义。
/// </summary>
public static class TruncatedBlockMetadataConstants
{
    public const string Reason = "reason";
    public const string CanContinue = "canContinue";

    public const string ReasonMaxTokens = "max_tokens";
    public const string ReasonMaxAutoContinuationsReached = "max_auto_continuations_reached";

    /// <summary>
    /// 规范化截断原因。
    /// </summary>
    public static string NormalizeReason(string? reason, string fallback = ReasonMaxTokens)
    {
        return reason?.Trim().ToLowerInvariant() switch
        {
            ReasonMaxTokens => ReasonMaxTokens,
            ReasonMaxAutoContinuationsReached => ReasonMaxAutoContinuationsReached,
            _ => fallback
        };
    }
}