using System.Net.Http.Json;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Client.Shared.Services.Api;

/// <summary>
/// REST API 服务 - 用户外部系统集成部分
/// </summary>
public partial class ApiService : IUserIntegrationApiService
{
    public async Task<IEnumerable<UserIntegrationResponse>> GetUserIntegrationsAsync(
        IntegrationType? type = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/user/integrations?includeInactive={includeInactive}";
        if (type.HasValue)
        {
            url += $"&type={type.Value}";
        }

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IEnumerable<UserIntegrationResponse>>(cancellationToken: cancellationToken)
            ?? Enumerable.Empty<UserIntegrationResponse>();
    }

    public async Task<IEnumerable<UserIntegrationDetailedResponse>> GetAllIntegrationsAsync(
        IntegrationType? type = null,
        bool includeInactive = false,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/user/integrations/all?includeInactive={includeInactive}";
        if (type.HasValue)
        {
            url += $"&type={type.Value}";
        }
        if (userId.HasValue)
        {
            url += $"&userId={userId.Value}";
        }

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IEnumerable<UserIntegrationDetailedResponse>>(cancellationToken: cancellationToken)
            ?? Enumerable.Empty<UserIntegrationDetailedResponse>();
    }

    public async Task<UserIntegrationResponse?> GetIntegrationByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/user/integrations/{id}";
        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<UserIntegrationResponse>(cancellationToken: cancellationToken);
    }

    public async Task<UserIntegrationResponse> CreateIntegrationAsync(
        CreateUserIntegrationRequest request,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/user/integrations";
        var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserIntegrationResponse>(cancellationToken: cancellationToken))!;
    }

    public async Task<UserIntegrationResponse> UpdateIntegrationAsync(
        Guid id,
        UpdateUserIntegrationRequest request,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/user/integrations/{id}";
        var response = await _httpClient.PutAsJsonAsync(url, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserIntegrationResponse>(cancellationToken: cancellationToken))!;
    }

    public async Task<bool> DeleteIntegrationAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/user/integrations/{id}";
        var response = await _httpClient.DeleteAsync(url, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SetAsDefaultIntegrationAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/user/integrations/{id}/set-default";
        var response = await _httpClient.PostAsync(url, null, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<ValidateUserIntegrationResponse> ValidateIntegrationAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/user/integrations/{id}/validate";
        var response = await _httpClient.PostAsync(url, null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ValidateUserIntegrationResponse>(cancellationToken: cancellationToken))!;
    }

    public async Task<ValidateUserIntegrationResponse> TestIntegrationConnectionAsync(
        ValidateUserIntegrationRequest request,
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/user/integrations/test";
        var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ValidateUserIntegrationResponse>(cancellationToken: cancellationToken))!;
    }

    public async Task<UserIntegrationStatsResponse> GetIntegrationStatsAsync(
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/v1/user/integrations/stats";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserIntegrationStatsResponse>(cancellationToken: cancellationToken))!;
    }
}
