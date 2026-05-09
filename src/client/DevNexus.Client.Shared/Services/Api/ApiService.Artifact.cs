using DevNexus.Client.Shared.Services.Exceptions;
using DevNexus.Shared.DTOs;
using System.Net;
using System.Net.Http.Json;
namespace DevNexus.Client.Shared.Services.Api;

/// <summary>
/// REST API 服务 - Artifact 部分
/// </summary>
public partial class ApiService : IArtifactApiService
{
    #region Artifact 管理

    /// <inheritdoc />
    public async Task<ArtifactDto> CreateArtifactAsync(CreateArtifactRequestDto request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/artifact", request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<ArtifactDto>()
            ?? throw new ApiException("创建 Artifact 失败");
    }

    /// <inheritdoc />
    public async Task<ArtifactDto?> GetArtifactAsync(Guid artifactId)
    {
        var response = await _httpClient.GetAsync($"/api/v1/artifact/{artifactId}");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<ArtifactDto>();
    }

    /// <inheritdoc />
    public async Task<List<ArtifactDto>> GetSessionArtifactsAsync(Guid sessionId)
    {
        var response = await _httpClient.GetAsync($"/api/v1/artifact/session/{sessionId}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<ArtifactDto>>() ?? new();
    }

    /// <inheritdoc />
    public async Task<List<ArtifactDto>> GetMessageArtifactsAsync(Guid messageId)
    {
        var response = await _httpClient.GetAsync($"/api/v1/artifact/message/{messageId}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<ArtifactDto>>() ?? new();
    }

    /// <inheritdoc />
    public async Task<ArtifactDto> UpdateArtifactAsync(Guid artifactId, string content)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/artifact/{artifactId}", new { Content = content });
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<ArtifactDto>()
            ?? throw new ApiException("更新 Artifact 失败");
    }

    /// <inheritdoc />
    public async Task DeleteArtifactAsync(Guid artifactId)
    {
        var response = await _httpClient.DeleteAsync($"/api/v1/artifact/{artifactId}");
        await EnsureSuccessAsync(response);
    }

    /// <summary>
    /// 解析文档内容（不创建 Artifact）
    /// </summary>
    public async Task<ParseDocumentResponse> ParseDocumentAsync(ParseDocumentRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/v1/artifact/parse", request);
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<ParseDocumentResponse>()
                ?? new ParseDocumentResponse { Success = false, ErrorMessage = "解析响应为空" };
        }
        catch (Exception ex)
        {
            return new ParseDocumentResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc />
    public async Task<ArtifactStatusDto?> GetParseStatusAsync(string traceId)
    {
        if (string.IsNullOrWhiteSpace(traceId))
        {
            return null;
        }

        var response = await _httpClient.GetAsync($"/api/v1/artifact/parse-status/{traceId}");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<ArtifactStatusDto>();
    }

    #endregion
}

