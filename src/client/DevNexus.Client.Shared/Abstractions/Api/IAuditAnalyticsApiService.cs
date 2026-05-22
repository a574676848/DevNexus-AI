using DevNexus.Client.Shared.DTOs;
using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// 审计分析 API 服务接口
/// </summary>
public interface IAuditAnalyticsApiService
{
    /// <summary>
    /// 获取当前用户 Token 统计
    /// </summary>
    Task<TokenUsageStatsDto> GetMyTokenStatsAsync(DateTime? startDate = null, DateTime? endDate = null);

    /// <summary>
    /// 获取审计中文字典。
    /// </summary>
    Task<AuditDictionaryDto> GetAuditDictionaryAsync();

    /// <summary>
    /// 获取审计看板数据。
    /// </summary>
    Task<AuditDashboardDto> GetAuditDashboardAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? ownerType = null,
        string? sceneCode = null,
        string? invocationKind = null,
        string? status = null);

    /// <summary>
    /// 获取当前用户 Token 使用记录
    /// </summary>
    Task<PagedResultDto<TokenUsageDto>> GetMyTokenRecordsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? ownerType = null,
        string? sceneCode = null,
        string? invocationKind = null,
        string? status = null,
        int pageNumber = 1,
        int pageSize = 50);

    /// <summary>
    /// 获取会话 Token 统计
    /// </summary>
    Task<TokenUsageStatsDto> GetSessionTokenStatsAsync(Guid sessionId);

    /// <summary>
    /// 获取系统整体 Token 统计（仅管理员）
    /// </summary>
    Task<TokenUsageStatsDto> GetSystemTokenStatsAsync(DateTime? startDate = null, DateTime? endDate = null);

    /// <summary>
    /// 获取系统整体 Token 统计（带筛选）。
    /// </summary>
    Task<TokenUsageStatsDto> GetSystemTokenStatsFilteredAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? ownerType = null,
        string? sceneCode = null,
        string? invocationKind = null,
        string? status = null);

    /// <summary>
    /// 获取所有用户 Token 使用记录（仅管理员）
    /// </summary>
    Task<PagedResultDto<TokenUsageDto>> GetAllTokenRecordsAsync(
        Guid? userId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int pageNumber = 1,
        int pageSize = 50);

    /// <summary>
    /// 获取指定用户 Token 统计（仅管理员）
    /// </summary>
    Task<TokenUsageStatsDto> GetUserTokenStatsAsync(Guid userId, DateTime? startDate = null, DateTime? endDate = null);

    /// <summary>
    /// 获取按供应商分组的统计（仅管理员）
    /// </summary>
    Task<List<ProviderUsageStatsDto>> GetProviderStatsAsync(DateTime? startDate = null, DateTime? endDate = null);

    /// <summary>
    /// 获取用户排行榜（仅管理员）
    /// </summary>
    Task<List<UserRankingDto>> GetUserRankingAsync(DateTime? startDate = null, DateTime? endDate = null, int topN = 10);

    /// <summary>
    /// 获取详细的 Token 使用记录（包含用户信息，仅管理员）
    /// </summary>
    Task<PagedResult<TokenUsageDetailedDto>> GetDetailedUsageRecordsAsync(
        Guid? userId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? ownerType = null,
        string? sceneCode = null,
        string? invocationKind = null,
        string? status = null,
        int pageNumber = 1,
        int pageSize = 50);
}

