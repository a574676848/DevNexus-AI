using DevNexus.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using AuditEntity = DevNexus.Domain.Entities.ModelInvocationAudit;

namespace DevNexus.Infrastructure.Services.Analytics;

/// <summary>
/// 审计分析服务使用记录查询能力。
/// </summary>
public partial class AuditAnalyticsService
{
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

        query = ApplyDateRange(query, startDate, endDate);

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(t => ToTokenUsageDto(t))
            .ToListAsync(cancellationToken);
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
        var records = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(t => ToTokenUsageDto(t))
            .ToListAsync(cancellationToken);

        return new PagedResult<TokenUsageDto>
        {
            Items = records,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    private static IQueryable<AuditEntity> ApplyDateRange(
        IQueryable<AuditEntity> query,
        DateTime? startDate,
        DateTime? endDate)
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

        return query;
    }

    private static TokenUsageDto ToTokenUsageDto(AuditEntity t)
    {
        return new TokenUsageDto
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
        };
    }
}
