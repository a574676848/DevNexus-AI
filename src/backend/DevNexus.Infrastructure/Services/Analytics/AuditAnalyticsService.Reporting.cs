using DevNexus.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Analytics;

/// <summary>
/// 审计分析服务报表与排名查询能力。
/// </summary>
public partial class AuditAnalyticsService
{
    /// <summary>
    /// 计算成本（使用 IModelPricingService 获取定价）。
    /// </summary>
    /// <param name="providerType">提供商类型</param>
    /// <param name="providerId">提供商数据库主键 ID（GUID 字符串）</param>
    private async Task<decimal> CalculateCostAsync(
        string providerType,
        string providerId,
        int inputTokens,
        int outputTokens,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!Guid.TryParse(providerId, out var providerGuid))
            {
                _logger.LogWarning("[AuditAnalytics] Invalid providerId format (not a GUID): {ProviderId}", providerId);
                return 0;
            }

            var normalizedProviderType = string.IsNullOrWhiteSpace(providerType)
                ? ModelInvocationProviderTypes.Llm
                : providerType;

            var pricing = await _pricingService.GetPricingByProviderAsync(
                normalizedProviderType,
                providerGuid,
                cancellationToken);

            if (pricing == null)
            {
                _logger.LogWarning(
                    "[AuditAnalytics] Pricing not found | ProviderType={ProviderType} ProviderId={ProviderId}",
                    normalizedProviderType,
                    providerId);
                return 0;
            }

            var inputCost = (decimal)inputTokens * pricing.InputCostPerMillion / 1_000_000m;
            var outputCost = normalizedProviderType == ModelInvocationProviderTypes.Embedding
                ? 0m
                : (decimal)outputTokens * pricing.OutputCostPerMillion / 1_000_000m;

            return inputCost + outputCost;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuditAnalytics] Failed to calculate cost");
            return 0;
        }
    }

    /// <inheritdoc />
    public async Task<List<ProviderUsageStatsDto>> GetProviderStatsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ModelInvocationAudits.AsQueryable();
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

        var records = await query.ToListAsync(cancellationToken);
        return records
            .GroupBy(r => new { r.ProviderType, r.ProviderId, r.ProviderName })
            .Select(g => new ProviderUsageStatsDto
            {
                ProviderType = g.Key.ProviderType,
                ProviderName = g.Key.ProviderName,
                ProviderId = g.Key.ProviderId,
                TotalRequests = g.Count(),
                TotalTokens = g.Sum(r => (long)(r.TotalTokens ?? 0)),
                TotalCost = g.Sum(r => r.Cost ?? 0),
                ModelBreakdown = g
                    .GroupBy(r => r.ModelId)
                    .Select(mg => new ModelUsageStatsDto
                    {
                        ModelId = mg.Key,
                        ProviderType = g.Key.ProviderType,
                        ProviderName = g.Key.ProviderName,
                        ProviderId = g.Key.ProviderId,
                        RequestCount = mg.Count(),
                        TotalTokens = mg.Sum(r => (long)(r.TotalTokens ?? 0)),
                        TotalCost = mg.Sum(r => r.Cost ?? 0)
                    })
                    .OrderByDescending(m => m.TotalTokens)
                    .ToList()
            })
            .OrderByDescending(p => p.TotalCost)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<List<UserRankingDto>> GetUserRankingAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        int topN = 10,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ModelInvocationAudits.AsQueryable();
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

        var userRanking = await query
            .Where(t => t.OwnerUserId != null)
            .GroupBy(t => t.OwnerUserId!.Value)
            .Select(g => new
            {
                UserId = g.Key,
                TotalTokens = g.Sum(t => (long)(t.TotalTokens ?? 0)),
                TotalCost = g.Sum(t => t.Cost ?? 0),
                SessionCount = g.Select(t => t.SessionId).Distinct().Count()
            })
            .OrderByDescending(u => u.TotalCost)
            .Take(topN)
            .ToListAsync(cancellationToken);

        var users = await _userIdentityService.GetUserInfosByIdsAsync(
            userRanking.Select(ranking => ranking.UserId),
            cancellationToken);

        var previousPeriodStart = startUtc?.AddDays(-(endUtc ?? DateTime.UtcNow).Subtract(startUtc.Value).Days);
        var previousPeriodEnd = startUtc;

        var previousCosts = await _dbContext.ModelInvocationAudits
            .Where(t => t.OwnerUserId != null && t.CreatedAt >= previousPeriodStart && t.CreatedAt < previousPeriodEnd)
            .GroupBy(t => t.OwnerUserId!.Value)
            .Select(g => new { UserId = g.Key, Cost = g.Sum(t => t.Cost ?? 0) })
            .ToListAsync(cancellationToken);

        return userRanking.Select(ur =>
        {
            users.TryGetValue(ur.UserId, out var user);
            var previousCost = previousCosts.FirstOrDefault(p => p.UserId == ur.UserId)?.Cost ?? 0;

            string trend = "stable";
            if (previousCost > 0)
            {
                var changePercent = ((ur.TotalCost - previousCost) / previousCost) * 100;
                if (changePercent > 10)
                {
                    trend = "up";
                }
                else if (changePercent < -10)
                {
                    trend = "down";
                }
            }
            else if (ur.TotalCost > 0)
            {
                trend = "up";
            }

            return new UserRankingDto
            {
                UserId = ur.UserId,
                Username = user?.Username ?? "Unknown",
                DisplayName = user?.DisplayName ?? user?.Username ?? "Unknown",
                TotalTokens = ur.TotalTokens,
                TotalCost = ur.TotalCost,
                SessionCount = ur.SessionCount,
                AverageCostPerSession = ur.SessionCount > 0 ? ur.TotalCost / ur.SessionCount : 0,
                Trend = trend
            };
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<PagedResult<TokenUsageDetailedDto>> GetDetailedUsageRecordsAsync(
        Guid? userId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? ownerType = null,
        string? sceneCode = null,
        string? invocationKind = null,
        string? status = null,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyAuditFilters(
            _dbContext.ModelInvocationAudits.AsQueryable(),
            userId,
            startDate,
            endDate,
            ownerType,
            sceneCode,
            invocationKind,
            status);

        var totalCount = await query.CountAsync(cancellationToken);
        var usageRecords = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var users = await _userIdentityService.GetUserInfosByIdsAsync(
            usageRecords.Where(record => record.OwnerUserId.HasValue).Select(record => record.OwnerUserId!.Value),
            cancellationToken);

        var records = usageRecords.Select(record =>
        {
            var userIdValue = record.OwnerUserId ?? record.UserId;
            if (userIdValue.HasValue)
            {
                users.TryGetValue(userIdValue.Value, out var user);
                return new TokenUsageDetailedDto
                {
                    Id = record.Id,
                    OwnerType = record.OwnerType,
                    OwnerUserId = record.OwnerUserId,
                    SessionId = record.SessionId,
                    MessageId = record.MessageId,
                    UserId = record.UserId,
                    Username = user?.Username ?? "System",
                    UserDisplayName = user?.DisplayName ?? user?.Username ?? "系统",
                    InvocationKind = record.InvocationKind,
                    SceneCode = record.SceneCode,
                    SceneCategory = record.SceneCategory,
                    ResourceType = record.ResourceType,
                    ResourceId = record.ResourceId,
                    ModelId = record.ModelId,
                    ProviderName = record.ProviderName,
                    ProviderId = record.ProviderId,
                    MeteringType = record.MeteringType,
                    InputTokens = record.InputTokens,
                    OutputTokens = record.OutputTokens,
                    TotalTokens = record.TotalTokens,
                    MeteringValue = record.MeteringValue,
                    DurationMs = record.DurationMs,
                    Cost = record.Cost,
                    RequestType = record.RequestType,
                    UsageSource = record.UsageSource,
                    Status = record.Status,
                    IsSuccess = record.IsSuccess,
                    ErrorCode = record.ErrorCode,
                    ErrorMessage = record.ErrorMessage,
                    StartedAt = record.StartedAt,
                    CompletedAt = record.CompletedAt,
                    CreatedAt = record.CreatedAt
                };
            }

            return new TokenUsageDetailedDto
            {
                Id = record.Id,
                OwnerType = record.OwnerType,
                OwnerUserId = record.OwnerUserId,
                SessionId = record.SessionId,
                MessageId = record.MessageId,
                UserId = record.UserId,
                Username = "System",
                UserDisplayName = "系统",
                InvocationKind = record.InvocationKind,
                SceneCode = record.SceneCode,
                SceneCategory = record.SceneCategory,
                ResourceType = record.ResourceType,
                ResourceId = record.ResourceId,
                ModelId = record.ModelId,
                ProviderName = record.ProviderName,
                ProviderId = record.ProviderId,
                MeteringType = record.MeteringType,
                InputTokens = record.InputTokens,
                OutputTokens = record.OutputTokens,
                TotalTokens = record.TotalTokens,
                MeteringValue = record.MeteringValue,
                DurationMs = record.DurationMs,
                Cost = record.Cost,
                RequestType = record.RequestType,
                UsageSource = record.UsageSource,
                Status = record.Status,
                IsSuccess = record.IsSuccess,
                ErrorCode = record.ErrorCode,
                ErrorMessage = record.ErrorMessage,
                StartedAt = record.StartedAt,
                CompletedAt = record.CompletedAt,
                CreatedAt = record.CreatedAt
            };
        }).ToList();

        return new PagedResult<TokenUsageDetailedDto>
        {
            Items = records,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
