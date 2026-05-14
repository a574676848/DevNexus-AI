using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 会话摘要服务。
/// </summary>
public sealed class SessionSummaryService : ISessionSummaryService
{
    private const string CacheKeyPrefix = "session_summary";
    private static readonly TimeSpan SummaryCacheTtl = TimeSpan.FromHours(12);

    private readonly ChatHistorySummaryService _summaryService;
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<SessionSummaryService> _logger;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public SessionSummaryService(
        ChatHistorySummaryService summaryService,
        IDistributedCache distributedCache,
        ILogger<SessionSummaryService> logger)
    {
        _summaryService = summaryService;
        _distributedCache = distributedCache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string?> GetOrCreateSummaryAsync(
        Guid sessionId,
        Guid providerId,
        string content,
        int targetChars,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var versionHash = PromptFingerprint.ComputeHash(content);
        var cacheKey = $"{CacheKeyPrefix}:{sessionId:N}:{versionHash}";
        var cached = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            _logger.LogDebug(
                "[AI.Chat] 命中会话摘要缓存 | SessionId={SessionId} Hash={Hash}",
                sessionId,
                versionHash);
            return cached;
        }

        var summary = await _summaryService.GetOrGenerateSummaryAsync(
            content,
            targetChars,
            sessionId,
            providerId,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(summary))
        {
            return null;
        }

        await _distributedCache.SetStringAsync(
            cacheKey,
            summary,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = SummaryCacheTtl
            },
            cancellationToken);

        return summary;
    }
}
