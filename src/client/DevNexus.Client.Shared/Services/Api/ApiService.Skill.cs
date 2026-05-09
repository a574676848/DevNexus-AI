using System.Net;
using System.Net.Http.Json;
using DevNexus.Shared.DTOs;
namespace DevNexus.Client.Shared.Services.Api;

public partial class ApiService : ISkillApiService
{
    /// <inheritdoc />
    public async Task<List<SkillDto>> GetAvailableSkillsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/api/v1/skill", cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<SkillDto>>(cancellationToken: cancellationToken) ?? new();
    }

    /// <inheritdoc />
    public async Task<SkillDetailResponse?> GetSkillDetailAsync(string name, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/v1/skill/{Uri.EscapeDataString(name)}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<SkillDetailResponse>(cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<SkillMatchTestResult>> TestMatchAsync(string message, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/v1/skill/match?message={Uri.EscapeDataString(message)}", cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<SkillMatchTestResult>>(cancellationToken: cancellationToken) ?? new();
    }

    /// <inheritdoc />
    public async Task<SkillReloadResponse> ReloadSkillsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync("/api/v1/skill/reload", null, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<SkillReloadResponse>(cancellationToken: cancellationToken) 
            ?? new SkillReloadResponse("热重载失败", 0, new());
    }

    /// <inheritdoc />
    public async Task<List<SkillDto>> UploadSkillAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        content.Add(streamContent, "file", fileName);

        var response = await _httpClient.PostAsync("/api/v1/skill/upload", content, cancellationToken);
        await EnsureSuccessAsync(response);
        
        var result = await response.Content.ReadFromJsonAsync<List<SkillDto>>(cancellationToken: cancellationToken);
        return result ?? new();
    }

    /// <inheritdoc />
    public async Task DeleteSkillAsync(string name, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"/api/v1/skill/{Uri.EscapeDataString(name)}", cancellationToken);
        await EnsureSuccessAsync(response);
    }
}

