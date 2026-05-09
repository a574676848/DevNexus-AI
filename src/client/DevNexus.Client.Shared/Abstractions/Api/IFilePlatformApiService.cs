using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// 文件平台 API 服务接口
/// </summary>
public interface IFilePlatformApiService
{
    /// <summary>
    /// 创建上传会话
    /// </summary>
    Task<CreateUploadSessionResponse> CreateUploadSessionAsync(CreateUploadSessionRequest request);

    /// <summary>
    /// 获取上传会话
    /// </summary>
    Task<UploadSessionDto?> GetUploadSessionAsync(Guid uploadSessionId);

    /// <summary>
    /// 上传上传会话内容
    /// </summary>
    Task UploadUploadSessionContentAsync(UploadSessionDto uploadSession, Stream fileStream, string contentType);

    /// <summary>
    /// 完成上传
    /// </summary>
    Task<FinalizeUploadResponse> FinalizeUploadAsync(FinalizeUploadRequest request);

    /// <summary>
    /// 获取文件资产
    /// </summary>
    Task<FileAssetDto?> GetFileAssetAsync(Guid fileAssetId);

    /// <summary>
    /// 批量获取文件资产
    /// </summary>
    Task<List<FileAssetDto>> GetFileAssetsByIdsAsync(List<Guid> fileAssetIds);

    /// <summary>
    /// 获取会话文件资产列表
    /// </summary>
    Task<List<FileAssetDto>> GetSessionFileAssetsAsync(Guid sessionId);

    /// <summary>
    /// 创建文件任务
    /// </summary>
    Task<FileTaskDto> CreateFileTaskAsync(CreateFileTaskRequest request);

    /// <summary>
    /// 判定是否应创建文件任务
    /// </summary>
    Task<FileTaskIntentDecisionResponse> DecideFileTaskIntentAsync(FileTaskIntentDecisionRequest request);

    /// <summary>
    /// 获取文件任务
    /// </summary>
    Task<FileTaskDto?> GetFileTaskAsync(Guid fileTaskId);

    /// <summary>
    /// 获取会话文件任务列表
    /// </summary>
    Task<List<FileTaskDto>> GetSessionFileTasksAsync(Guid sessionId);

    /// <summary>
    /// 重试文件任务
    /// </summary>
    Task<FileTaskDto> RetryFileTaskAsync(Guid fileTaskId);

    /// <summary>
    /// 取消文件任务
    /// </summary>
    Task<FileTaskDto> CancelFileTaskAsync(Guid fileTaskId);
}
