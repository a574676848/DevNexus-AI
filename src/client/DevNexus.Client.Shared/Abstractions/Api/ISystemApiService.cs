using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// 系统服务 API 接口（系统信息、代码分析、文件存储）
/// </summary>
public interface ISystemApiService
{
    #region 系统信息

    /// <summary>
    /// 获取客户端版本信息
    /// </summary>
    Task<ClientVersionDto?> GetClientVersionAsync();

    /// <summary>
    /// 请求客户端更新 manifest。
    /// </summary>
    /// <param name="request">更新决策请求。</param>
    /// <returns>更新 manifest。</returns>
    Task<UpdateManifestResponse?> GetUpdateManifestAsync(UpdateManifestRequest request);

    /// <summary>
    /// 上报客户端更新事件。
    /// </summary>
    Task ReportUpdateClientEventAsync(ReportUpdateClientEventRequest request);

    /// <summary>
    /// 获取系统健康状态
    /// </summary>
    Task<HealthResponseDto> GetHealthAsync();

    /// <summary>
    /// 获取服务器信息（管理员）
    /// </summary>
    Task<ServerInfoResponseDto?> GetServerInfoAsync();

    #endregion

    #region 更新策略管理（管理员）

    /// <summary>
    /// 获取发布版本列表。
    /// </summary>
    Task<IReadOnlyList<ReleaseDto>> GetReleasesAsync();

    /// <summary>
    /// 保存发布版本。
    /// </summary>
    Task<ReleaseDto?> SaveReleaseAsync(SaveReleaseRequest request);

    /// <summary>
    /// 导入发布元数据。
    /// </summary>
    Task<ImportReleaseMetadataResult?> ImportReleaseMetadataAsync(ImportReleaseMetadataRequest request);

    /// <summary>
    /// 发布指定版本。
    /// </summary>
    Task<ReleaseDto?> PublishReleaseAsync(Guid releaseId);

    /// <summary>
    /// 归档指定版本。
    /// </summary>
    Task<ReleaseDto?> ArchiveReleaseAsync(Guid releaseId);

    /// <summary>
    /// 删除指定版本。
    /// </summary>
    Task DeleteReleaseAsync(Guid releaseId);

    /// <summary>
    /// 获取投放规则列表。
    /// </summary>
    Task<IReadOnlyList<RolloutDto>> GetRolloutsAsync();

    /// <summary>
    /// 保存投放规则。
    /// </summary>
    Task<RolloutDto?> SaveRolloutAsync(SaveRolloutRequest request);

    /// <summary>
    /// 暂停投放规则。
    /// </summary>
    Task<RolloutDto?> PauseRolloutAsync(Guid rolloutId);

    /// <summary>
    /// 恢复投放规则。
    /// </summary>
    Task<RolloutDto?> ResumeRolloutAsync(Guid rolloutId);

    /// <summary>
    /// 回滚投放规则。
    /// </summary>
    Task<RolloutDto?> RollbackRolloutAsync(Guid rolloutId);

    /// <summary>
    /// 删除投放规则。
    /// </summary>
    Task DeleteRolloutAsync(Guid rolloutId);

    /// <summary>
    /// 预演更新命中结果。
    /// </summary>
    Task<UpdateManifestResponse?> PreviewRolloutAsync(UpdateManifestRequest request);

    /// <summary>
    /// 获取更新观测摘要。
    /// </summary>
    Task<UpdateObservabilitySummaryDto?> GetUpdateObservabilitySummaryAsync();

    /// <summary>
    /// 获取更新观测详情。
    /// </summary>
    Task<UpdateObservabilityDetailDto?> GetUpdateObservabilityDetailsAsync(UpdateObservabilityFilterRequest request);

    #endregion

    #region 文件存储

    /// <summary>
    /// 获取预签名上传 URL
    /// </summary>
    Task<PresignedUploadResponse> GetPresignedUploadUrlAsync(PresignedUploadRequest request);

    /// <summary>
    /// 上传文件（仅本地存储模式）
    /// </summary>
    Task<FileUploadResultDto> UploadFileAsync(Stream fileStream, string objectKey, string contentType);

    /// <summary>
    /// 确认文件上传完成
    /// </summary>
    Task<FileUploadResultDto> ConfirmUploadAsync(string objectKey);

    /// <summary>
    /// 获取存储服务信息
    /// </summary>
    Task<StorageInfoDto> GetStorageInfoAsync();

    /// <summary>
    /// 智能上传文件（自动根据存储模式选择正确的上传方式）
    /// Local 模式：通过服务端上传
    /// S3 模式：使用预签名 URL 直接上传到存储服务
    /// </summary>
    /// <param name="fileStream">文件流</param>
    /// <param name="fileName">文件名</param>
    /// <param name="contentType">内容类型</param>
    /// <param name="folder">文件夹（可选）</param>
    /// <returns>上传结果</returns>
    Task<FileUploadResultDto> SmartUploadFileAsync(Stream fileStream, string fileName, string contentType, string? folder = null);

    #endregion
}

