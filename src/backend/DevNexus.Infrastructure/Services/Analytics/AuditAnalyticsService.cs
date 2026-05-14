// using DevNexus.Domain.Abstractions via GlobalUsings
// using DevNexus.Domain.Entities via GlobalUsings
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
                CachedPromptTokens = record.CachedPromptTokens,
                StablePrefixHash = record.StablePrefixHash,
                ToolSchemaHash = record.ToolSchemaHash,
                DynamicContextTokens = record.DynamicContextTokens,
                HistoryTokens = record.HistoryTokens,
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
    public async Task<AiOptimizationDashboardDto> GetAiOptimizationDashboardAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ModelInvocationAudits.AsNoTracking();
        var startUtc = EnsureUtc(startDate);
        var endUtc = EnsureUtc(endDate);

        if (startUtc.HasValue)
        {
            query = query.Where(item => item.CreatedAt >= startUtc.Value);
        }

        if (endUtc.HasValue)
        {
            var endOfDay = endUtc.Value.Date.AddDays(1);
            query = query.Where(item => item.CreatedAt < endOfDay);
        }

        var totalInputTokens = await query.SumAsync(item => (long)(item.InputTokens ?? 0), cancellationToken);
        var cachedPromptTokens = await query.SumAsync(item => (long)(item.CachedPromptTokens ?? 0), cancellationToken);
        var stablePrefixTrackedRequests = await query.CountAsync(
            item => item.StablePrefixHash != null && item.StablePrefixHash != string.Empty,
            cancellationToken);
        var toolSchemaTrackedRequests = await query.CountAsync(
            item => item.ToolSchemaHash != null && item.ToolSchemaHash != string.Empty,
            cancellationToken);

        var toolQuery = query.Where(item => item.InvocationKind == ModelInvocationKinds.FunctionCall && item.ToolName != null);
        var toolCallCount = await toolQuery.CountAsync(cancellationToken);
        var toolSuccessCount = await toolQuery.CountAsync(item => item.IsSuccess, cancellationToken);
        var toolArgumentValidCount = await toolQuery.CountAsync(item => item.ToolArgumentsValid == true, cancellationToken);
        var toolRetryableFailureCount = await toolQuery.CountAsync(item => !item.IsSuccess && item.ToolRetryable == true, cancellationToken);
        var toolHumanInterventionCount = await toolQuery.CountAsync(item => item.ToolRequiresHumanIntervention == true, cancellationToken);

        var dashboard = new AiOptimizationDashboardDto
        {
            TotalInputTokens = totalInputTokens,
            CachedPromptTokens = cachedPromptTokens,
            CacheHitRatio = totalInputTokens > 0 ? (double)cachedPromptTokens / totalInputTokens : 0,
            StablePrefixTrackedRequests = stablePrefixTrackedRequests,
            ToolSchemaTrackedRequests = toolSchemaTrackedRequests,
            ToolCallCount = toolCallCount,
            ToolSuccessCount = toolSuccessCount,
            ToolFailureCount = toolCallCount - toolSuccessCount,
            ToolSuccessRate = toolCallCount > 0 ? (double)toolSuccessCount / toolCallCount : 0,
            ToolArgumentValidCount = toolArgumentValidCount,
            ToolRetryableFailureCount = toolRetryableFailureCount,
            ToolHumanInterventionCount = toolHumanInterventionCount
        };

        dashboard.ToolStats = await toolQuery
            .GroupBy(item => item.ToolName!)
            .Select(group => new ToolInvocationStatsDto
            {
                ToolName = group.Key,
                RequestCount = group.Count(),
                SuccessCount = group.Count(item => item.IsSuccess),
                FailureCount = group.Count(item => !item.IsSuccess),
                SuccessRate = group.Count() > 0 ? (double)group.Count(item => item.IsSuccess) / group.Count() : 0,
                AverageDurationMs = group.Average(item => (double)item.DurationMs)
            })
            .OrderByDescending(item => item.RequestCount)
            .Take(20)
            .ToListAsync(cancellationToken);

        dashboard.ToolFailureReasonStats = await toolQuery
            .Where(item => !item.IsSuccess && item.ToolFailureReason != null)
            .GroupBy(item => item.ToolFailureReason!)
            .Select(group => new AuditBreakdownDto
            {
                Code = group.Key,
                DisplayName = group.Key,
                RequestCount = group.Count(),
                FailedCount = group.Count()
            })
            .OrderByDescending(item => item.RequestCount)
            .ToListAsync(cancellationToken);

        return dashboard;
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
    public async Task<List<TokenUsageDto>> GetUsageRecordsAsync(
        Guid? userId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ModelInvocationAudits.AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(t => t.OwnerUserId == userId.Value);
        }

        var startUtc = EnsureUtc(startDate);
        var endUtc = EnsureUtc(endDate);

        if (startUtc.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= startUtc.Value);
        }

        if (endUtc.HasValue)
        {
            // 包含当天的所有记录
            var endOfDay = endUtc.Value.Date.AddDays(1);
            query = query.Where(t => t.CreatedAt < endOfDay);
        }

        var records = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TokenUsageDto
            {
                Id = t.Id,
                OwnerType = t.OwnerType,
                OwnerUserId = t.OwnerUserId,
                SessionId = t.SessionId,
                MessageId = t.MessageId,
                UserId = t.UserId,
                InvocationKind = t.InvocationKind,
                SceneCode = t.SceneCode,
                SceneCategory = t.SceneCategory,
                ResourceType = t.ResourceType,
                ResourceId = t.ResourceId,
                ModelId = t.ModelId,
                ProviderName = t.ProviderName,
                ProviderId = t.ProviderId,
                MeteringType = t.MeteringType,
                InputTokens = t.InputTokens,
                OutputTokens = t.OutputTokens,
                TotalTokens = t.TotalTokens,
                CachedPromptTokens = t.CachedPromptTokens,
                StablePrefixHash = t.StablePrefixHash,
                ToolSchemaHash = t.ToolSchemaHash,
                DynamicContextTokens = t.DynamicContextTokens,
                HistoryTokens = t.HistoryTokens,
                ToolName = t.ToolName,
                ToolArgumentsValid = t.ToolArgumentsValid,
                ToolFailureReason = t.ToolFailureReason,
                ToolSuggestedAction = t.ToolSuggestedAction,
                ToolRetryable = t.ToolRetryable,
                ToolRequiresHumanIntervention = t.ToolRequiresHumanIntervention,
                ToolExitCode = t.ToolExitCode,
                MeteringValue = t.MeteringValue,
                DurationMs = t.DurationMs,
                Cost = t.Cost,
                RequestType = t.RequestType,
                UsageSource = t.UsageSource,
                Status = t.Status,
                IsSuccess = t.IsSuccess,
                ErrorCode = t.ErrorCode,
                ErrorMessage = t.ErrorMessage,
                StartedAt = t.StartedAt,
                CompletedAt = t.CompletedAt,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return records;
    }

    /// <inheritdoc />
    public async Task<PagedResult<TokenUsageDto>> GetUsageRecordsPagedAsync(
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
        var query = _dbContext.ModelInvocationAudits.AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(t => t.OwnerUserId == userId.Value || t.UserId == userId.Value);
        }

        if (!string.IsNullOrWhiteSpace(ownerType))
        {
            query = query.Where(t => t.OwnerType == ownerType);
        }

        if (!string.IsNullOrWhiteSpace(sceneCode))
        {
            query = query.Where(t => t.SceneCode == sceneCode);
        }

        if (!string.IsNullOrWhiteSpace(invocationKind))
        {
            query = query.Where(t => t.InvocationKind == invocationKind);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(t => t.Status == status);
        }

        var startUtc = EnsureUtc(startDate);
        var endUtc = EnsureUtc(endDate);

        if (startUtc.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= startUtc.Value);
        }

        if (endUtc.HasValue)
        {
            // 包含当天的所有记录
            var endOfDay = endUtc.Value.Date.AddDays(1);
            query = query.Where(t => t.CreatedAt < endOfDay);
        }

        // 获取总数
        var totalCount = await query.CountAsync(cancellationToken);

        // 获取分页数据
        var records = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TokenUsageDto
            {
                Id = t.Id,
                OwnerType = t.OwnerType,
                OwnerUserId = t.OwnerUserId,
                SessionId = t.SessionId,
                MessageId = t.MessageId,
                UserId = t.UserId,
                InvocationKind = t.InvocationKind,
                SceneCode = t.SceneCode,
                SceneCategory = t.SceneCategory,
                ResourceType = t.ResourceType,
                ResourceId = t.ResourceId,
                ModelId = t.ModelId,
                ProviderName = t.ProviderName,
                ProviderId = t.ProviderId,
                MeteringType = t.MeteringType,
                InputTokens = t.InputTokens,
                OutputTokens = t.OutputTokens,
                TotalTokens = t.TotalTokens,
                CachedPromptTokens = t.CachedPromptTokens,
                StablePrefixHash = t.StablePrefixHash,
                ToolSchemaHash = t.ToolSchemaHash,
                DynamicContextTokens = t.DynamicContextTokens,
                HistoryTokens = t.HistoryTokens,
                ToolName = t.ToolName,
                ToolArgumentsValid = t.ToolArgumentsValid,
                ToolFailureReason = t.ToolFailureReason,
                ToolSuggestedAction = t.ToolSuggestedAction,
                ToolRetryable = t.ToolRetryable,
                ToolRequiresHumanIntervention = t.ToolRequiresHumanIntervention,
                ToolExitCode = t.ToolExitCode,
                MeteringValue = t.MeteringValue,
                DurationMs = t.DurationMs,
                Cost = t.Cost,
                RequestType = t.RequestType,
                UsageSource = t.UsageSource,
                Status = t.Status,
                IsSuccess = t.IsSuccess,
                ErrorCode = t.ErrorCode,
                ErrorMessage = t.ErrorMessage,
                StartedAt = t.StartedAt,
                CompletedAt = t.CompletedAt,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<TokenUsageDto>
        {
            Items = records,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
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
