using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Prompt 成本观测输入。
/// </summary>
public sealed record PromptCostObservation
{
    /// <summary>
    /// 输入 Token 数。
    /// </summary>
    public int? InputTokens { get; init; }

    /// <summary>
    /// Provider 返回的缓存命中 Prompt Token 数。
    /// </summary>
    public int? CachedPromptTokens { get; init; }

    /// <summary>
    /// 动态上下文 Token 数。
    /// </summary>
    public int? DynamicContextTokens { get; init; }

    /// <summary>
    /// 历史消息 Token 数。
    /// </summary>
    public int? HistoryTokens { get; init; }
}

/// <summary>
/// Prompt 成本诊断快照。
/// </summary>
public sealed record PromptCostDiagnosticsSnapshot
{
    /// <summary>
    /// 未命中缓存的输入 Token 数。
    /// </summary>
    public int? NonCachedInputTokens { get; init; }

    /// <summary>
    /// 缓存命中输入占比，范围为 0 到 1。
    /// </summary>
    public decimal? CacheHitRatio { get; init; }

    /// <summary>
    /// 动态上下文占输入 Token 的比例，范围为 0 到 1。
    /// </summary>
    public decimal? DynamicContextRatio { get; init; }

    /// <summary>
    /// 历史消息占输入 Token 的比例，范围为 0 到 1。
    /// </summary>
    public decimal? HistoryRatio { get; init; }
}

/// <summary>
/// Prompt 成本诊断工具。
/// </summary>
public static class PromptCostDiagnostics
{
    private const int MinimumTokenCount = 0;

    /// <summary>
    /// 根据单次真实请求观测值构建成本诊断快照。
    /// </summary>
    public static PromptCostDiagnosticsSnapshot Build(PromptCostObservation observation)
    {
        var inputTokens = NormalizeTokenCount(observation.InputTokens);
        var cachedPromptTokens = NormalizeTokenCount(observation.CachedPromptTokens);
        var dynamicContextTokens = NormalizeTokenCount(observation.DynamicContextTokens);
        var historyTokens = NormalizeTokenCount(observation.HistoryTokens);

        return new PromptCostDiagnosticsSnapshot
        {
            NonCachedInputTokens = TokenUsageMetrics.CalculateNonCachedInputTokens(
                inputTokens,
                cachedPromptTokens),
            CacheHitRatio = CalculateRatio(cachedPromptTokens, inputTokens),
            DynamicContextRatio = CalculateRatio(dynamicContextTokens, inputTokens),
            HistoryRatio = CalculateRatio(historyTokens, inputTokens)
        };
    }

    private static int? NormalizeTokenCount(int? tokenCount)
    {
        return tokenCount.HasValue
            ? Math.Max(MinimumTokenCount, tokenCount.Value)
            : null;
    }

    private static decimal? CalculateRatio(int? part, int? total)
    {
        if (!part.HasValue || !total.HasValue || total.Value <= MinimumTokenCount)
        {
            return null;
        }

        var normalizedPart = Math.Clamp(part.Value, MinimumTokenCount, total.Value);
        return decimal.Divide(normalizedPart, total.Value);
    }
}
