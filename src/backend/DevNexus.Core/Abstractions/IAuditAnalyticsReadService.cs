using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Abstractions;

public interface IAuditAnalyticsReadService
{
    Task<TokenUsageStatsDto> GetUserStatsAsync(
        Guid userId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);

    Task<TokenUsageStatsDto> GetSystemStatsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? ownerType = null,
        string? sceneCode = null,
        string? invocationKind = null,
        string? status = null,
        CancellationToken cancellationToken = default);

    Task<AuditDictionaryDto> GetAuditDictionaryAsync(CancellationToken cancellationToken = default);

    Task<AuditDashboardDto> GetAuditDashboardAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? ownerType = null,
        string? sceneCode = null,
        string? invocationKind = null,
        string? status = null,
        CancellationToken cancellationToken = default);

    Task<List<TokenUsageDto>> GetUsageRecordsAsync(
        Guid? userId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<PagedResult<TokenUsageDto>> GetUsageRecordsPagedAsync(
        Guid? userId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? ownerType = null,
        string? sceneCode = null,
        string? invocationKind = null,
        string? status = null,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<TokenUsageStatsDto> GetSessionStatsAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<List<ProviderUsageStatsDto>> GetProviderStatsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);

    Task<List<UserRankingDto>> GetUserRankingAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        int topN = 10,
        CancellationToken cancellationToken = default);

    Task<PagedResult<TokenUsageDetailedDto>> GetDetailedUsageRecordsAsync(
        Guid? userId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? ownerType = null,
        string? sceneCode = null,
        string? invocationKind = null,
        string? status = null,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
}
