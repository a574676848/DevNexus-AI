// using DevNexus.Domain.Abstractions via GlobalUsings
// using DevNexus.Domain.Entities via GlobalUsings
using DevNexus.Core.Services.Chat;
using DevNexus.Shared.DTOs;
using DevNexus.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AuditEntity = DevNexus.Domain.Entities.ModelInvocationAudit;

namespace DevNexus.Infrastructure.Services.Analytics;

/// <summary>
/// 审计分析服务实现
/// </summary>
public partial class AuditAnalyticsService : IAuditAnalyticsReadService, IAuditAnalyticsWriteService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IUserIdentityService _userIdentityService;
    private readonly ILogger<AuditAnalyticsService> _logger;
    private readonly IModelPricingService _pricingService;

    public AuditAnalyticsService(
        ApplicationDbContext dbContext,
        IUserIdentityService userIdentityService,
        ILogger<AuditAnalyticsService> logger,
        IModelPricingService pricingService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _userIdentityService = userIdentityService ?? throw new ArgumentNullException(nameof(userIdentityService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pricingService = pricingService ?? throw new ArgumentNullException(nameof(pricingService));
    }

    /// <inheritdoc />
    public async Task RecordUsageAsync(
        ModelInvocationAuditRecord record,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(record);

            var inputTokens = record.InputTokens ?? 0;
            var outputTokens = record.OutputTokens ?? 0;
            var totalTokens = record.TotalTokens ?? (inputTokens + outputTokens);
            var isTokenMetering = string.Equals(record.MeteringType, ModelInvocationMeteringTypes.Token, StringComparison.OrdinalIgnoreCase);
            var cost = isTokenMetering
                ? await CalculateCostAsync(record.ProviderType, record.ProviderId, inputTokens, outputTokens, cancellationToken)
                : 0m;
            var ownerUserId = record.OwnerUserId;

            var tokenUsage = new AuditEntity
            {
                OwnerType = record.OwnerType,
                OwnerUserId = ownerUserId,
                UserId = ownerUserId,
                InvocationKind = record.InvocationKind,
                SceneCode = record.SceneCode,
                SceneCategory = record.SceneCategory,
                ResourceType = record.ResourceType,
                ResourceId = record.ResourceId,
                SessionId = record.SessionId,
                MessageId = record.MessageId,
                TraceId = record.TraceId,
                ParentInvocationId = record.ParentInvocationId,
                RootInvocationId = record.RootInvocationId,
                ModelId = record.ModelId,
                ProviderType = record.ProviderType,
                ProviderName = record.ProviderName,
                ProviderId = record.ProviderId,
                MeteringType = record.MeteringType,
                InputTokens = record.InputTokens,
                OutputTokens = record.OutputTokens,
                TotalTokens = totalTokens,
                ToolName = record.ToolName,
                ToolArgumentsValid = record.ToolArgumentsValid,
                ToolFailureReason = record.ToolFailureReason,
                ToolSuggestedAction = record.ToolSuggestedAction,
                ToolRetryable = record.ToolRetryable,
                ToolRequiresHumanIntervention = record.ToolRequiresHumanIntervention,
                ToolExitCode = record.ToolExitCode,
                MeteringValue = record.MeteringValue ?? totalTokens,
                DurationMs = record.DurationMs,
                Cost = record.Cost ?? cost,
                RequestType = record.InvocationKind,
                UsageSource = record.UsageSource,
                Status = record.Status,
                IsSuccess = string.Equals(record.Status, ModelInvocationStatuses.Succeeded, StringComparison.OrdinalIgnoreCase),
                ErrorCode = record.ErrorCode,
                ErrorMessage = record.ErrorMessage,
                StartedAt = record.StartedAt,
                CompletedAt = record.CompletedAt ?? record.StartedAt.AddMilliseconds(record.DurationMs)
            };

            await _dbContext.ModelInvocationAudits.AddAsync(tokenUsage, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "[AuditAnalytics] Recorded | UserId={UserId} Model={Model} Provider={ProviderId} " +
                "SceneCode={SceneCode} InputTokens={InputTokens} OutputTokens={OutputTokens} Cost=${Cost:F4}",
                ownerUserId, record.ModelId, record.ProviderId, record.SceneCode, inputTokens, outputTokens, tokenUsage.Cost);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[AuditAnalytics] Failed to record usage | SessionId={SessionId} MessageId={MessageId} " +
                "UserId={UserId} ModelId={ModelId} ProviderId={ProviderId} ExceptionType={ExceptionType}",
                record.SessionId, record.MessageId, record.OwnerUserId, record.ModelId, record.ProviderId, ex.GetType().Name);
            // 不抛出异常，避免影响主流程
        }
    }

    /// <inheritdoc />
    public async Task<TokenUsageStatsDto> GetUserStatsAsync(
        Guid userId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ModelInvocationAudits
            .Where(t => t.OwnerUserId == userId || t.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        _logger.LogInformation(
            "[AuditAnalytics] Querying user stats | UserId={UserId} TotalRecords={TotalRecords} StartDate={StartDate} EndDate={EndDate}",
            userId,
            totalCount,
            startDate,
            endDate);

        return await GetStatsFromQueryAsync(query, startDate, endDate, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TokenUsageStatsDto> GetSystemStatsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? ownerType = null,
        string? sceneCode = null,
        string? invocationKind = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyAuditFilters(
            _dbContext.ModelInvocationAudits.AsNoTracking(),
            userId: null,
            startDate,
            endDate,
            ownerType,
            sceneCode,
            invocationKind,
            status);

        return await GetStatsFromQueryAsync(query, null, null, cancellationToken);
    }

    /// <inheritdoc />
    public Task<AuditDictionaryDto> GetAuditDictionaryAsync(CancellationToken cancellationToken = default)
    {
        return GetAuditDictionaryInternalAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AuditDashboardDto> GetAuditDashboardAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? ownerType = null,
        string? sceneCode = null,
        string? invocationKind = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyAuditFilters(
            _dbContext.ModelInvocationAudits.AsNoTracking(),
            userId: null,
            startDate,
            endDate,
            ownerType,
            sceneCode,
            invocationKind,
            status);

        var totalRequests = await query.CountAsync(cancellationToken);
        var totalTokens = await query.SumAsync(item => (long)(item.TotalTokens ?? 0), cancellationToken);
        var totalCost = await query.SumAsync(item => item.Cost ?? 0, cancellationToken);
        var successCount = await query.CountAsync(item => item.IsSuccess, cancellationToken);
        var timeoutCount = await query.CountAsync(item => item.Status == ModelInvocationStatuses.Timeout, cancellationToken);
        var estimatedCount = await query.CountAsync(item => item.UsageSource == ModelInvocationUsageSources.Estimated, cancellationToken);
        var systemCount = await query.CountAsync(item => item.OwnerType == ModelInvocationOwnerTypes.System, cancellationToken);
        var averageDuration = totalRequests == 0
            ? 0
            : await query.Select(item => (double)item.DurationMs).AverageAsync(cancellationToken);

        var dictionary = await GetAuditDictionaryInternalAsync(cancellationToken);
        var sceneLookup = dictionary.Scenes.ToDictionary(item => item.Code, item => item.DisplayName, StringComparer.OrdinalIgnoreCase);

        var sceneBreakdown = await query
            .GroupBy(item => item.SceneCode)
            .Select(group => new AuditBreakdownDto
            {
                Code = group.Key,
                DisplayName = group.Key,
                RequestCount = group.Count(),
                TotalTokens = group.Sum(item => (long)(item.TotalTokens ?? 0)),
                TotalCost = group.Sum(item => item.Cost ?? 0),
                FailedCount = group.Count(item => !item.IsSuccess)
            })
            .OrderByDescending(item => item.TotalCost)
            .ToListAsync(cancellationToken);

        foreach (var item in sceneBreakdown)
        {
            if (sceneLookup.TryGetValue(item.Code, out var displayName))
            {
                item.DisplayName = displayName;
            }
        }

        var ownerBreakdown = await query
            .GroupBy(item => item.OwnerType)
            .Select(group => new AuditBreakdownDto
            {
                Code = group.Key,
                DisplayName = group.Key == ModelInvocationOwnerTypes.User ? "用户" : "系统",
                RequestCount = group.Count(),
                TotalTokens = group.Sum(item => (long)(item.TotalTokens ?? 0)),
                TotalCost = group.Sum(item => item.Cost ?? 0),
                FailedCount = group.Count(item => !item.IsSuccess)
            })
            .OrderByDescending(item => item.TotalCost)
            .ToListAsync(cancellationToken);

        var invocationBreakdown = await query
            .GroupBy(item => item.InvocationKind)
            .Select(group => new AuditBreakdownDto
            {
                Code = group.Key,
                DisplayName = group.Key,
                RequestCount = group.Count(),
                TotalTokens = group.Sum(item => (long)(item.TotalTokens ?? 0)),
                TotalCost = group.Sum(item => item.Cost ?? 0),
                FailedCount = group.Count(item => !item.IsSuccess)
            })
            .OrderByDescending(item => item.TotalCost)
            .ToListAsync(cancellationToken);

        var exceptionSpots = await query
            .GroupBy(item => item.SceneCode)
            .Select(group => new AuditExceptionSpotDto
            {
                SceneCode = group.Key,
                DisplayName = group.Key,
                FailedCount = group.Count(item => !item.IsSuccess),
                TimeoutCount = group.Count(item => item.Status == ModelInvocationStatuses.Timeout),
                EstimatedCount = group.Count(item => item.UsageSource == ModelInvocationUsageSources.Estimated),
                TotalCount = group.Count()
            })
            .ToListAsync(cancellationToken);

        foreach (var item in exceptionSpots)
        {
            if (sceneLookup.TryGetValue(item.SceneCode, out var displayName))
            {
                item.DisplayName = displayName;
            }

            item.FailureRate = item.TotalCount == 0 ? 0 : (double)item.FailedCount / item.TotalCount;
            item.TimeoutRate = item.TotalCount == 0 ? 0 : (double)item.TimeoutCount / item.TotalCount;
            item.EstimatedRate = item.TotalCount == 0 ? 0 : (double)item.EstimatedCount / item.TotalCount;
        }

        exceptionSpots = exceptionSpots
            .OrderByDescending(item => item.FailureRate)
            .ThenByDescending(item => item.TimeoutRate)
            .ThenByDescending(item => item.EstimatedRate)
            .Take(8)
            .ToList();

        return new AuditDashboardDto
        {
            Overview = new AuditOverviewDto
            {
                TotalRequests = totalRequests,
                TotalTokens = totalTokens,
                TotalCost = totalCost,
                SuccessRate = totalRequests == 0 ? 0 : (double)successCount / totalRequests,
                AverageDurationMs = averageDuration,
                SystemCallRatio = totalRequests == 0 ? 0 : (double)systemCount / totalRequests,
                EstimatedUsageRatio = totalRequests == 0 ? 0 : (double)estimatedCount / totalRequests,
                TimeoutRate = totalRequests == 0 ? 0 : (double)timeoutCount / totalRequests
            },
            SceneBreakdown = sceneBreakdown,
            OwnerBreakdown = ownerBreakdown,
            InvocationBreakdown = invocationBreakdown,
            ExceptionSpots = exceptionSpots
        };
    }

    /// <inheritdoc />
    public async Task<TokenUsageStatsDto> GetSessionStatsAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ModelInvocationAudits
            .Where(t => t.SessionId == sessionId);

        return await GetStatsFromQueryAsync(query, null, null, cancellationToken);
    }

    /// <summary>
    /// 从查询中获取统计数据
    /// </summary>
    private async Task<TokenUsageStatsDto> GetStatsFromQueryAsync(
        IQueryable<AuditEntity> query,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken)
    {
        var startUtc = EnsureUtc(startDate);
        var endUtc = EnsureUtc(endDate);

        if (startUtc.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= startUtc.Value);
        }

        if (endUtc.HasValue)
        {
            var endOfDay = endUtc.Value.Date.AddDays(1);
            query = query.Where(t => t.CreatedAt < endOfDay);
        }

        // 基础统计数据获取
        var totalRequests = await query.CountAsync(cancellationToken);
        if (totalRequests == 0)
        {
            return new TokenUsageStatsDto();
        }

        var successfulRequests = await query.CountAsync(t => t.IsSuccess, cancellationToken);
        var totalInputTokens = await query.SumAsync(t => (long)(t.InputTokens ?? 0), cancellationToken);
        var totalOutputTokens = await query.SumAsync(t => (long)(t.OutputTokens ?? 0), cancellationToken);
        var totalTokens = await query.SumAsync(t => (long)(t.TotalTokens ?? 0), cancellationToken);
        var totalCost = await query.SumAsync(t => t.Cost ?? 0, cancellationToken);
        var avgDuration = await query.Select(t => (double)t.DurationMs).AverageAsync(cancellationToken);

        var stats = new TokenUsageStatsDto
        {
            TotalRequests = totalRequests,
            SuccessfulRequests = successfulRequests,
            FailedRequests = totalRequests - successfulRequests,
            TotalInputTokens = totalInputTokens,
            TotalOutputTokens = totalOutputTokens,
            TotalTokens = totalTokens,
            TotalCost = totalCost,
            AverageDurationMs = avgDuration
        };

        // 按模型分组统计 (数据库端分组)
        stats.ModelStats = await query
            .GroupBy(r => new { r.ModelId, r.ProviderType, r.ProviderName, r.ProviderId })
            .Select(g => new ModelUsageStatsDto
            {
                ModelId = g.Key.ModelId,
                ProviderType = g.Key.ProviderType,
                ProviderName = g.Key.ProviderName,
                ProviderId = g.Key.ProviderId,
                RequestCount = g.Count(),
                TotalTokens = g.Sum(r => (long)(r.TotalTokens ?? 0)),
                TotalCost = g.Sum(r => r.Cost ?? 0)
            })
            .OrderByDescending(m => m.TotalTokens)
            .ToListAsync(cancellationToken);

        // 按日期分组统计 (数据库端分组)
        stats.DailyStats = await query
            .GroupBy(r => r.CreatedAt.Date)
            .Select(g => new DailyUsageStatsDto
            {
                Date = g.Key,
                RequestCount = g.Count(),
                TotalTokens = g.Sum(r => (long)(r.TotalTokens ?? 0)),
                TotalCost = g.Sum(r => r.Cost ?? 0)
            })
            .OrderBy(d => d.Date)
            .ToListAsync(cancellationToken);

        stats.SceneStats = await query
            .GroupBy(r => new { r.SceneCode, r.SceneCategory })
            .Select(g => new AuditBreakdownDto
            {
                Code = g.Key.SceneCode,
                DisplayName = g.Key.SceneCode,
                RequestCount = g.Count(),
                TotalTokens = g.Sum(r => (long)(r.TotalTokens ?? 0)),
                TotalCost = g.Sum(r => r.Cost ?? 0),
                FailedCount = g.Count(r => !r.IsSuccess)
            })
            .OrderByDescending(s => s.TotalTokens)
            .ToListAsync(cancellationToken);

        var dictionary = await GetAuditDictionaryInternalAsync(cancellationToken);
        var sceneLookup = dictionary.Scenes.ToDictionary(item => item.Code, item => item.DisplayName, StringComparer.OrdinalIgnoreCase);
        foreach (var sceneStat in stats.SceneStats)
        {
            if (sceneLookup.TryGetValue(sceneStat.Code, out var displayName))
            {
                sceneStat.DisplayName = displayName;
            }
        }

        stats.OwnerStats = await query
            .GroupBy(r => r.OwnerType)
            .Select(g => new AuditBreakdownDto
            {
                Code = g.Key,
                DisplayName = g.Key == ModelInvocationOwnerTypes.User ? "用户" : "系统",
                RequestCount = g.Count(),
                TotalTokens = g.Sum(r => (long)(r.TotalTokens ?? 0)),
                TotalCost = g.Sum(r => r.Cost ?? 0),
                FailedCount = g.Count(r => !r.IsSuccess)
            })
            .OrderByDescending(s => s.TotalTokens)
            .ToListAsync(cancellationToken);

        stats.InvocationKindStats = await query
            .GroupBy(r => r.InvocationKind)
            .Select(g => new AuditBreakdownDto
            {
                Code = g.Key,
                DisplayName = g.Key,
                RequestCount = g.Count(),
                TotalTokens = g.Sum(r => (long)(r.TotalTokens ?? 0)),
                TotalCost = g.Sum(r => r.Cost ?? 0),
                FailedCount = g.Count(r => !r.IsSuccess)
            })
            .OrderByDescending(s => s.TotalTokens)
            .ToListAsync(cancellationToken);

        return stats;
    }

}
