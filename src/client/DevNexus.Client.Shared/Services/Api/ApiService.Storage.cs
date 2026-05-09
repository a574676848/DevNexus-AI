using DevNexus.Client.Shared.DTOs;
using DevNexus.Shared.DTOs;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DevNexus.Client.Shared.Services.Exceptions;

namespace DevNexus.Client.Shared.Services.Api;

/// <summary>
/// REST API 服务 - 文件存储部分
/// </summary>
public partial class ApiService
{
    #region 文件存储

    /// <inheritdoc />
    public async Task<PresignedUploadResponse> GetPresignedUploadUrlAsync(PresignedUploadRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/storage/presigned-upload-url", request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<PresignedUploadResponse>()
            ?? throw new ApiException("获取上传URL失败");
    }

    /// <inheritdoc />
    public async Task<FileUploadResultDto> UploadFileAsync(Stream fileStream, string objectKey, string contentType)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", Path.GetFileName(objectKey));

        var response = await _httpClient.PostAsync($"/api/v1/storage/upload?objectKey={Uri.EscapeDataString(objectKey)}", content);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<FileUploadResultDto>()
            ?? new FileUploadResultDto { ObjectKey = objectKey, Confirmed = true };
    }

    /// <inheritdoc />
    public async Task<FileUploadResultDto> ConfirmUploadAsync(string objectKey)
    {
        var response = await _httpClient.PostAsync($"/api/v1/storage/confirm-upload?objectKey={Uri.EscapeDataString(objectKey)}", null);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<FileUploadResultDto>()
            ?? new FileUploadResultDto { ObjectKey = objectKey, Confirmed = true };
    }

    /// <inheritdoc />
    public async Task<StorageInfoDto> GetStorageInfoAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/v1/storage/info");
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<StorageInfoDto>()
                ?? new StorageInfoDto { Provider = "Unknown" };
        }
        catch
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<FileUploadResultDto> SmartUploadFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string? folder = null)
    {
        // 1. 获取预签名上传信息，包含上传方式和 URL
        var presignedRequest = new PresignedUploadRequest
        {
            FileName = fileName,
            ContentType = contentType,
            Folder = folder
        };
        var presignedResponse = await GetPresignedUploadUrlAsync(presignedRequest);

        // 2. 根据上传方式选择正确的上传方法
        if (presignedResponse.UploadMethod == "Server")
        {
            // 本地存储模式：通过服务端上传
            return await UploadFileAsync(fileStream, presignedResponse.ObjectKey, contentType);
        }
        else
        {
            // S3 模式：使用预签名 URL 直接上传
            return await UploadToPresignedUrlAsync(fileStream, presignedResponse, contentType);
        }
    }

    /// <summary>
    /// 使用预签名 URL 直接上传到存储服务（S3 模式）
    /// </summary>
    private async Task<FileUploadResultDto> UploadToPresignedUrlAsync(
        Stream fileStream,
        PresignedUploadResponse presignedInfo,
        string contentType)
    {
        using var httpClient = _httpClientFactory.CreateClient("DirectUpload");
        using var content = new StreamContent(fileStream);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        // PUT 请求到预签名 URL
        var response = await httpClient.PutAsync(presignedInfo.UploadUrl, content);

        if (!response.IsSuccessStatusCode)
        {
            throw new ApiException($"上传文件失败: {response.StatusCode}");
        }

        return new FileUploadResultDto
        {
            ObjectKey = presignedInfo.ObjectKey,
            FileUrl = presignedInfo.FileUrl,
            Confirmed = true
        };
    }

    #endregion
}
