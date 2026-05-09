using System.Net.Http.Json;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;
namespace DevNexus.Client.Shared.Services.Api;

/// <summary>
/// REST API 服务 - 系统信息和代码分析部分
/// </summary>
public partial class ApiService
{
    #region 系统信息

    /// <inheritdoc />
    public async Task<ClientVersionDto?> GetClientVersionAsync()
    {
        try
        {
            var response = await GetUpdateManifestAsync(new UpdateManifestRequest
            {
                Platform = _clientEnvironmentService.UpdatePlatform,
                Architecture = _clientEnvironmentService.Architecture,
                CurrentVersion = string.Empty,
                OsVersion = _clientEnvironmentService.OsVersion
            });

            if (response?.TargetRelease == null)
            {
                return null;
            }

            var artifact = response.Artifacts.FirstOrDefault();

            return new ClientVersionDto(
                response.TargetRelease.Version,
                artifact?.DownloadUrl ?? string.Empty,
                response.TargetRelease.ReleaseNotes,
                response.Mandatory);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<UpdateManifestResponse?> GetUpdateManifestAsync(UpdateManifestRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/update/manifest", request);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<UpdateManifestResponse>();
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task ReportUpdateClientEventAsync(ReportUpdateClientEventRequest request)
    {
        try
        {
            await _httpClient.PostAsJsonAsync("/api/update/events", request);
        }
        catch
        {
            // 观测事件不阻断主流程
        }
    }

    /// <inheritdoc />
    public async Task<HealthResponseDto> GetHealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/v1/system/health");
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<HealthResponseDto>() ?? new();
        }
        catch
        {
            return new HealthResponseDto { Status = "Unknown" };
        }
    }

    /// <inheritdoc />
    public async Task<ServerInfoResponseDto?> GetServerInfoAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/v1/system/info");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ServerInfoResponseDto>();
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region 更新策略管理（管理员）

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReleaseDto>> GetReleasesAsync()
    {
        var response = await _httpClient.GetAsync("/api/v1/admin/releases");
        await EnsureSuccessAsync(response);
        return await ReadApiResponseAsync<IReadOnlyList<ReleaseDto>>(response) ?? Array.Empty<ReleaseDto>();
    }

    /// <inheritdoc />
    public async Task<ReleaseDto?> SaveReleaseAsync(SaveReleaseRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/admin/releases", request);
        await EnsureSuccessAsync(response);
        return await ReadApiResponseAsync<ReleaseDto>(response);
    }

    /// <inheritdoc />
    public async Task<ImportReleaseMetadataResult?> ImportReleaseMetadataAsync(ImportReleaseMetadataRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/admin/releases/import-metadata", request);
        await EnsureSuccessAsync(response);
        return await ReadApiResponseAsync<ImportReleaseMetadataResult>(response);
    }

    /// <inheritdoc />
    public async Task<ReleaseDto?> PublishReleaseAsync(Guid releaseId)
    {
        var response = await _httpClient.PostAsync($"/api/v1/admin/releases/{releaseId}/publish", null);
        await EnsureSuccessAsync(response);
        return await ReadApiResponseAsync<ReleaseDto>(response);
    }

    /// <inheritdoc />
    public async Task<ReleaseDto?> ArchiveReleaseAsync(Guid releaseId)
    {
        var response = await _httpClient.PostAsync($"/api/v1/admin/releases/{releaseId}/archive", null);
        await EnsureSuccessAsync(response);
        return await ReadApiResponseAsync<ReleaseDto>(response);
    }

    /// <inheritdoc />
    public async Task DeleteReleaseAsync(Guid releaseId)
    {
        var response = await _httpClient.DeleteAsync($"/api/v1/admin/releases/{releaseId}");
        await EnsureSuccessAsync(response);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RolloutDto>> GetRolloutsAsync()
    {
        var response = await _httpClient.GetAsync("/api/v1/admin/rollouts");
        await EnsureSuccessAsync(response);
        return await ReadApiResponseAsync<IReadOnlyList<RolloutDto>>(response) ?? Array.Empty<RolloutDto>();
    }

    /// <inheritdoc />
    public async Task<RolloutDto?> SaveRolloutAsync(SaveRolloutRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/admin/rollouts", request);
        await EnsureSuccessAsync(response);
        return await ReadApiResponseAsync<RolloutDto>(response);
    }

    /// <inheritdoc />
    public async Task<RolloutDto?> PauseRolloutAsync(Guid rolloutId)
    {
        var response = await _httpClient.PostAsync($"/api/v1/admin/rollouts/{rolloutId}/pause", null);
        await EnsureSuccessAsync(response);
        return await ReadApiResponseAsync<RolloutDto>(response);
    }

    /// <inheritdoc />
    public async Task<RolloutDto?> ResumeRolloutAsync(Guid rolloutId)
    {
        var response = await _httpClient.PostAsync($"/api/v1/admin/rollouts/{rolloutId}/resume", null);
        await EnsureSuccessAsync(response);
        return await ReadApiResponseAsync<RolloutDto>(response);
    }

    /// <inheritdoc />
    public async Task<RolloutDto?> RollbackRolloutAsync(Guid rolloutId)
    {
        var response = await _httpClient.PostAsync($"/api/v1/admin/rollouts/{rolloutId}/rollback", null);
        await EnsureSuccessAsync(response);
        return await ReadApiResponseAsync<RolloutDto>(response);
    }

    /// <inheritdoc />
    public async Task DeleteRolloutAsync(Guid rolloutId)
    {
        var response = await _httpClient.DeleteAsync($"/api/v1/admin/rollouts/{rolloutId}");
        await EnsureSuccessAsync(response);
    }

    /// <inheritdoc />
    public async Task<UpdateManifestResponse?> PreviewRolloutAsync(UpdateManifestRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/admin/rollouts/preview", request);
        await EnsureSuccessAsync(response);
        return await ReadApiResponseAsync<UpdateManifestResponse>(response);
    }

    /// <inheritdoc />
    public async Task<UpdateObservabilitySummaryDto?> GetUpdateObservabilitySummaryAsync()
    {
        var response = await _httpClient.GetAsync("/api/v1/admin/update-observability/summary");
        await EnsureSuccessAsync(response);
        return await ReadApiResponseAsync<UpdateObservabilitySummaryDto>(response);
    }

    /// <inheritdoc />
    public async Task<UpdateObservabilityDetailDto?> GetUpdateObservabilityDetailsAsync(UpdateObservabilityFilterRequest request)
    {
        var queryParts = new List<string>
        {
            $"days={request.Days}"
        };

        if (request.ReleaseId.HasValue)
        {
            queryParts.Add($"releaseId={request.ReleaseId.Value}");
        }

        if (request.RolloutId.HasValue)
        {
            queryParts.Add($"rolloutId={request.RolloutId.Value}");
        }

        if (!string.IsNullOrWhiteSpace(request.EventType))
        {
            queryParts.Add($"eventType={Uri.EscapeDataString(request.EventType)}");
        }

        if (!string.IsNullOrWhiteSpace(request.Result))
        {
            queryParts.Add($"result={Uri.EscapeDataString(request.Result)}");
        }

        var response = await _httpClient.GetAsync($"/api/v1/admin/update-observability/details?{string.Join("&", queryParts)}");
        await EnsureSuccessAsync(response);
        return await ReadApiResponseAsync<UpdateObservabilityDetailDto>(response);
    }

    #endregion
}
