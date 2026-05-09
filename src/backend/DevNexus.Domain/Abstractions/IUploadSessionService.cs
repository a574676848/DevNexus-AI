using DevNexus.Shared.DTOs;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 上传会话服务接口
/// </summary>
public interface IUploadSessionService
{
    /// <summary>
    /// 创建上传会话
    /// </summary>
    Task<CreateUploadSessionResponse> CreateUploadSessionAsync(
        Guid userId,
        CreateUploadSessionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 完成上传
    /// </summary>
    Task<FinalizeUploadResponse> FinalizeUploadAsync(
        Guid userId,
        FinalizeUploadRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取上传会话
    /// </summary>
    Task<UploadSessionDto?> GetUploadSessionAsync(
        Guid userId,
        Guid uploadSessionId,
        CancellationToken cancellationToken = default);
}