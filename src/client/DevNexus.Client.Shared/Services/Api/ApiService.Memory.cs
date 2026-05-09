using System.Net.Http.Json;
using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Services.Api;

/// <summary>
/// REST API 服务 - 记忆管理部分
/// </summary>
public partial class ApiService : IMemoryApiService
{
    #region 记忆管理

    /// <inheritdoc />
    public async Task<List<UserFactDto>> GetUserFactsAsync()
    {
        var response = await _httpClient.GetAsync("/api/v1/memory/facts");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<UserFactDto>>()
            ?? new List<UserFactDto>();
    }

    /// <inheritdoc />
    public async Task<UserFactDto> AddUserFactAsync(AddUserFactRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/memory/facts", request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<UserFactDto>()
            ?? throw new Exception("添加用户画像失败");
    }

    /// <inheritdoc />
    public async Task DeleteUserFactAsync(Guid factId)
    {
        var response = await _httpClient.DeleteAsync($"/api/v1/memory/facts/{factId}");
        await EnsureSuccessAsync(response);
    }

    /// <inheritdoc />
    public async Task TogglePinFactAsync(Guid factId)
    {
        var response = await _httpClient.PostAsync($"/api/v1/memory/facts/{factId}/toggle-pin", null);
        await EnsureSuccessAsync(response);
    }

    /// <inheritdoc />
    public async Task<List<EpisodicMemoryDto>> GetMemoryTimelineAsync(int page = 1, int pageSize = 20)
    {
        var response = await _httpClient.GetAsync($"/api/v1/memory/timeline?page={page}&pageSize={pageSize}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<EpisodicMemoryDto>>()
            ?? new List<EpisodicMemoryDto>();
    }

    /// <inheritdoc />
    public async Task<List<EpisodicMemoryDto>> SearchMemoriesAsync(string query, int topK = 20)
    {
        var encodedQuery = Uri.EscapeDataString(query);
        var response = await _httpClient.GetAsync($"/api/v1/memory/search?query={encodedQuery}&topK={topK}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<EpisodicMemoryDto>>()
            ?? new List<EpisodicMemoryDto>();
    }

    // ==============================================
    // 智能体系统经验管理 (System 1/2 Shared Memory)
    // ==============================================

    /// <inheritdoc />
    public async Task<PagedResult<SystemExperienceDto>> GetSystemExperiencesAsync(DevNexus.Shared.Enums.ExperienceType? type, string? search, int page = 1, int pageSize = 20)
    {
        var url = $"/api/v1/memory/system?page={page}&pageSize={pageSize}";
        if (type.HasValue) url += $"&type={type.Value}";
        if (!string.IsNullOrWhiteSpace(search)) url += $"&search={Uri.EscapeDataString(search)}";

        var response = await _httpClient.GetAsync(url);
        await EnsureSuccessAsync(response);
        
        return await response.Content.ReadFromJsonAsync<PagedResult<SystemExperienceDto>>()
            ?? new PagedResult<SystemExperienceDto> { Items = new(), TotalCount = 0, PageNumber = page, PageSize = pageSize };
    }

    /// <inheritdoc />
    public async Task TogglePinSystemExperienceAsync(Guid id)
    {
        var response = await _httpClient.PutAsync($"/api/v1/memory/system/{id}/pin", null);
        await EnsureSuccessAsync(response);
    }

    /// <inheritdoc />
    public async Task UpdateSystemExperienceScoreAsync(Guid id, double score)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/memory/system/{id}/score", score);
        await EnsureSuccessAsync(response);
    }

    /// <inheritdoc />
    public async Task DeleteSystemExperienceAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"/api/v1/memory/system/{id}");
        await EnsureSuccessAsync(response);
    }

    #endregion
}
