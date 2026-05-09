using DevNexus.Client.Shared.Services.Exceptions;
using DevNexus.Shared.DTOs;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace DevNexus.Client.Shared.Services.Api;

/// <summary>
/// REST API 服务 - 文件平台部分
/// </summary>
public partial class ApiService : IFilePlatformApiService
{
    /// <inheritdoc />
    public async Task<CreateUploadSessionResponse> CreateUploadSessionAsync(CreateUploadSessionRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/uploads/sessions", request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<CreateUploadSessionResponse>()
            ?? throw new ApiException("创建上传会话失败");
    }

    /// <inheritdoc />
    public async Task<UploadSessionDto?> GetUploadSessionAsync(Guid uploadSessionId)
    {
        var response = await _httpClient.GetAsync($"/api/v1/uploads/sessions/{uploadSessionId}");
        if (response.StatusCode == global::System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<UploadSessionDto>();
    }

    /// <inheritdoc />
    public async Task UploadUploadSessionContentAsync(UploadSessionDto uploadSession, Stream fileStream, string contentType)
    {
        if (uploadSession.UploadMethod == "Server")
        {
            await UploadToServerEndpointAsync(uploadSession.UploadUrl, fileStream, contentType, uploadSession.ObjectKey);
            return;
        }

        await UploadToPresignedUrlAsync(uploadSession.UploadUrl, fileStream, contentType);
    }

    /// <inheritdoc />
    public async Task<FinalizeUploadResponse> FinalizeUploadAsync(FinalizeUploadRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/uploads/sessions/finalize", request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<FinalizeUploadResponse>()
            ?? throw new ApiException("完成上传失败");
    }

    /// <inheritdoc />
    public async Task<FileAssetDto?> GetFileAssetAsync(Guid fileAssetId)
    {
        var response = await _httpClient.GetAsync($"/api/v1/file-assets/{fileAssetId}");
        if (response.StatusCode == global::System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<FileAssetDto>();
    }

    /// <inheritdoc />
    public async Task<List<FileAssetDto>> GetFileAssetsByIdsAsync(List<Guid> fileAssetIds)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/file-assets/batch", fileAssetIds);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<FileAssetDto>>() ?? new List<FileAssetDto>();
    }

    /// <inheritdoc />
    public async Task<List<FileAssetDto>> GetSessionFileAssetsAsync(Guid sessionId)
    {
        var response = await _httpClient.GetAsync($"/api/v1/file-assets/session/{sessionId}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<FileAssetDto>>() ?? new List<FileAssetDto>();
    }

    /// <inheritdoc />
    public async Task<FileTaskDto> CreateFileTaskAsync(CreateFileTaskRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/file-tasks", request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<FileTaskDto>()
            ?? throw new ApiException("创建文件任务失败");
    }

    /// <inheritdoc />
    public async Task<FileTaskIntentDecisionResponse> DecideFileTaskIntentAsync(FileTaskIntentDecisionRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/file-tasks/intents/decide", request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<FileTaskIntentDecisionResponse>()
            ?? throw new ApiException("文件任务意图判定失败");
    }

    /// <inheritdoc />
    public async Task<FileTaskDto?> GetFileTaskAsync(Guid fileTaskId)
    {
        var response = await _httpClient.GetAsync($"/api/v1/file-tasks/{fileTaskId}");
        if (response.StatusCode == global::System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<FileTaskDto>();
    }

    /// <inheritdoc />
    public async Task<List<FileTaskDto>> GetSessionFileTasksAsync(Guid sessionId)
    {
        var response = await _httpClient.GetAsync($"/api/v1/file-tasks/session/{sessionId}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<FileTaskDto>>() ?? new List<FileTaskDto>();
    }

    /// <inheritdoc />
    public async Task<FileTaskDto> RetryFileTaskAsync(Guid fileTaskId)
    {
        var response = await _httpClient.PostAsync($"/api/v1/file-tasks/{fileTaskId}/retry", content: null);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<FileTaskDto>()
            ?? throw new ApiException("重试文件任务失败");
    }

    /// <inheritdoc />
    public async Task<FileTaskDto> CancelFileTaskAsync(Guid fileTaskId)
    {
        var response = await _httpClient.PostAsync($"/api/v1/file-tasks/{fileTaskId}/cancel", content: null);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<FileTaskDto>()
            ?? throw new ApiException("取消文件任务失败");
    }

    private async Task UploadToServerEndpointAsync(string uploadUrl, Stream fileStream, string contentType, string objectKey)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", Path.GetFileName(objectKey));

        var response = await _httpClient.PostAsync(uploadUrl, content);
        await EnsureSuccessAsync(response);
    }

    private async Task UploadToPresignedUrlAsync(string uploadUrl, Stream fileStream, string contentType)
    {
        using var httpClient = _httpClientFactory.CreateClient("DirectUpload");
        using var content = new StreamContent(fileStream);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var response = await httpClient.PutAsync(uploadUrl, content);
        if (!response.IsSuccessStatusCode)
        {
            throw new ApiException($"上传文件失败: {response.StatusCode}");
        }
    }
}
