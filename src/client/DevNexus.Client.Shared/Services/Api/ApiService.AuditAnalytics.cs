using System.Net.Http.Json;
using DevNexus.Client.Shared.DTOs;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;
namespace DevNexus.Client.Shared.Services.Api;

/// <summary>
/// REST API 服务 - 审计分析部分
/// </summary>
public partial class ApiService
{
    #region 审计分析

    /// <inheritdoc />
    public async Task<TokenUsageStatsDto> GetMyTokenStatsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = BuildDateQuery(startDate, endDate);
        var response = await _httpClient.GetAsync($"/api/v1/auditanalytics/my-stats{query}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TokenUsageStatsDto>() ?? new();
    }

    /// <inheritdoc />
    public async Task<AuditDictionaryDto> GetAuditDictionaryAsync()
    {
        var response = await _httpClient.GetAsync("/api/v1/auditanalytics/dictionary");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<AuditDictionaryDto>() ?? new();
    }

    /// <inheritdoc />
    public async Task<AuditDashboardDto> GetAuditDashboardAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? ownerType = null,
        string? sceneCode = null,
        string? invocationKind = null,
        string? status = null)
    {
        var queryParts = new List<string>();
        if (startDate.HasValue)
            queryParts.Add($"startDate={startDate.Value:yyyy-MM-dd}");
        if (endDate.HasValue)
            queryParts.Add($"endDate={endDate.Value:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(ownerType))
            queryParts.Add($"ownerType={Uri.EscapeDataString(ownerType)}");
        if (!string.IsNullOrWhiteSpace(sceneCode))
            queryParts.Add($"sceneCode={Uri.EscapeDataString(sceneCode)}");
        if (!string.IsNullOrWhiteSpace(invocationKind))
            queryParts.Add($"invocationKind={Uri.EscapeDataString(invocationKind)}");
        if (!string.IsNullOrWhiteSpace(status))
            queryParts.Add($"status={Uri.EscapeDataString(status)}");

        var query = queryParts.Count > 0 ? "?" + string.Join("&", queryParts) : string.Empty;
        var response = await _httpClient.GetAsync($"/api/v1/auditanalytics/dashboard{query}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<AuditDashboardDto>() ?? new();
    }

    /// <inheritdoc />
    public async Task<PagedResultDto<TokenUsageDto>> GetMyTokenRecordsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? ownerType = null,
        string? sceneCode = null,
        string? invocationKind = null,
        string? status = null,
        int pageNumber = 1,
        int pageSize = 50)
    {
        var queryParts = new List<string>();
        if (startDate.HasValue)
            queryParts.Add($"startDate={startDate.Value:yyyy-MM-dd}");
        if (endDate.HasValue)
            queryParts.Add($"endDate={endDate.Value:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(ownerType))
            queryParts.Add($"ownerType={Uri.EscapeDataString(ownerType)}");
        if (!string.IsNullOrWhiteSpace(sceneCode))
            queryParts.Add($"sceneCode={Uri.EscapeDataString(sceneCode)}");
        if (!string.IsNullOrWhiteSpace(invocationKind))
            queryParts.Add($"invocationKind={Uri.EscapeDataString(invocationKind)}");
        if (!string.IsNullOrWhiteSpace(status))
            queryParts.Add($"status={Uri.EscapeDataString(status)}");
        queryParts.Add($"pageNumber={pageNumber}");
        queryParts.Add($"pageSize={pageSize}");

        var query = "?" + string.Join("&", queryParts);

        var response = await _httpClient.GetAsync($"/api/v1/auditanalytics/my-records{query}");
        await EnsureSuccessAsync(response);

        // 后端返回 PagedResult，前端转换为 PagedResultDto
        var result = await response.Content.ReadFromJsonAsync<PagedResult<TokenUsageDto>>() ?? new();

        return new PagedResultDto<TokenUsageDto>
        {
            Items = result.Items,
            TotalCount = result.TotalCount,
            PageNumber = result.PageNumber,
            PageSize = result.PageSize
        };
    }

    /// <inheritdoc />
    public async Task<TokenUsageStatsDto> GetSessionTokenStatsAsync(Guid sessionId)
    {
        var response = await _httpClient.GetAsync($"/api/v1/auditanalytics/session/{sessionId}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TokenUsageStatsDto>() ?? new();
    }

    /// <inheritdoc />
    public async Task<TokenUsageStatsDto> GetSystemTokenStatsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = BuildDateQuery(startDate, endDate);
        var response = await _httpClient.GetAsync($"/api/v1/auditanalytics/system-stats{query}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TokenUsageStatsDto>() ?? new();
    }

    /// <inheritdoc />
    public async Task<TokenUsageStatsDto> GetSystemTokenStatsFilteredAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? ownerType = null,
        string? sceneCode = null,
        string? invocationKind = null,
        string? status = null)
    {
        var queryParts = new List<string>();
        if (startDate.HasValue)
            queryParts.Add($"startDate={startDate.Value:yyyy-MM-dd}");
        if (endDate.HasValue)
            queryParts.Add($"endDate={endDate.Value:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(ownerType))
            queryParts.Add($"ownerType={Uri.EscapeDataString(ownerType)}");
        if (!string.IsNullOrWhiteSpace(sceneCode))
            queryParts.Add($"sceneCode={Uri.EscapeDataString(sceneCode)}");
        if (!string.IsNullOrWhiteSpace(invocationKind))
            queryParts.Add($"invocationKind={Uri.EscapeDataString(invocationKind)}");
        if (!string.IsNullOrWhiteSpace(status))
            queryParts.Add($"status={Uri.EscapeDataString(status)}");

        var query = queryParts.Count > 0 ? "?" + string.Join("&", queryParts) : string.Empty;
        var response = await _httpClient.GetAsync($"/api/v1/auditanalytics/system-stats{query}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TokenUsageStatsDto>() ?? new();
    }

    /// <inheritdoc />
    public async Task<PagedResultDto<TokenUsageDto>> GetAllTokenRecordsAsync(
        Guid? userId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int pageNumber = 1,
        int pageSize = 50)
    {
        var queryParts = new List<string>();
        if (userId.HasValue)
            queryParts.Add($"userId={userId.Value}");
        if (startDate.HasValue)
            queryParts.Add($"startDate={startDate.Value:yyyy-MM-dd}");
        if (endDate.HasValue)
            queryParts.Add($"endDate={endDate.Value:yyyy-MM-dd}");
        queryParts.Add($"pageNumber={pageNumber}");
        queryParts.Add($"pageSize={pageSize}");

        var query = "?" + string.Join("&", queryParts);
        var response = await _httpClient.GetAsync($"/api/v1/auditanalytics/all-records{query}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<PagedResultDto<TokenUsageDto>>() ?? new();
    }

    /// <inheritdoc />
    public async Task<TokenUsageStatsDto> GetUserTokenStatsAsync(Guid userId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = BuildDateQuery(startDate, endDate);
        var response = await _httpClient.GetAsync($"/api/v1/auditanalytics/user/{userId}/stats{query}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TokenUsageStatsDto>() ?? new();
    }

    /// <inheritdoc />
    public async Task<List<ProviderUsageStatsDto>> GetProviderStatsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = BuildDateQuery(startDate, endDate);
        var response = await _httpClient.GetAsync($"/api/v1/auditanalytics/provider-stats{query}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<ProviderUsageStatsDto>>() ?? new();
    }

    /// <inheritdoc />
    public async Task<List<UserRankingDto>> GetUserRankingAsync(DateTime? startDate = null, DateTime? endDate = null, int topN = 10)
    {
        var queryParts = new List<string>();
        if (startDate.HasValue)
            queryParts.Add($"startDate={startDate.Value:yyyy-MM-dd}");
        if (endDate.HasValue)
            queryParts.Add($"endDate={endDate.Value:yyyy-MM-dd}");
        queryParts.Add($"topN={topN}");

        var query = queryParts.Count > 0 ? "?" + string.Join("&", queryParts) : "";
        var response = await _httpClient.GetAsync($"/api/v1/auditanalytics/user-ranking{query}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<UserRankingDto>>() ?? new();
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
        int pageSize = 50)
    {
        var queryParts = new List<string>();
        if (userId.HasValue)
            queryParts.Add($"userId={userId.Value}");
        if (startDate.HasValue)
            queryParts.Add($"startDate={startDate.Value:yyyy-MM-dd}");
        if (endDate.HasValue)
            queryParts.Add($"endDate={endDate.Value:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(ownerType))
            queryParts.Add($"ownerType={Uri.EscapeDataString(ownerType)}");
        if (!string.IsNullOrWhiteSpace(sceneCode))
            queryParts.Add($"sceneCode={Uri.EscapeDataString(sceneCode)}");
        if (!string.IsNullOrWhiteSpace(invocationKind))
            queryParts.Add($"invocationKind={Uri.EscapeDataString(invocationKind)}");
        if (!string.IsNullOrWhiteSpace(status))
            queryParts.Add($"status={Uri.EscapeDataString(status)}");
        queryParts.Add($"pageNumber={pageNumber}");
        queryParts.Add($"pageSize={pageSize}");

        var query = "?" + string.Join("&", queryParts);
        var response = await _httpClient.GetAsync($"/api/v1/auditanalytics/all-records-detailed{query}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<PagedResult<TokenUsageDetailedDto>>() ?? new();
    }

    #endregion
}

