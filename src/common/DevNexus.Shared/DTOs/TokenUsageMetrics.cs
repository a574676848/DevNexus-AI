namespace DevNexus.Shared.DTOs;

/// <summary>
/// Token 使用量派生指标。
/// </summary>
public static class TokenUsageMetrics
{
    /// <summary>
    /// 计算未命中缓存的输入 Token 数。
    /// </summary>
    public static int? CalculateNonCachedInputTokens(int? inputTokens, int? cachedPromptTokens)
    {
        if (!inputTokens.HasValue)
        {
            return null;
        }

        return Math.Max(0, inputTokens.Value - (cachedPromptTokens ?? 0));
    }

    /// <summary>
    /// 计算未命中缓存的输入 Token 总数。
    /// </summary>
    public static long CalculateNonCachedInputTokens(long inputTokens, long cachedPromptTokens)
    {
        return Math.Max(0, inputTokens - cachedPromptTokens);
    }
}
