using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Services;

/// <summary>
/// 发布中心管理服务。
/// </summary>
public interface IUpdateReleaseManagementService
{
    /// <summary>
    /// 获取全部发布版本。
    /// </summary>
    Task<IReadOnlyList<ReleaseDto>> GetReleasesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存发布版本。
    /// </summary>
    Task<ReleaseDto> SaveReleaseAsync(SaveReleaseRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发布指定版本。
    /// </summary>
    Task<ReleaseDto> PublishReleaseAsync(Guid releaseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 归档指定版本。
    /// </summary>
    Task<ReleaseDto> ArchiveReleaseAsync(Guid releaseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除指定版本。
    /// </summary>
    Task DeleteReleaseAsync(Guid releaseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 导入发布元数据并按需生成投放规则。
    /// </summary>
    Task<ImportReleaseMetadataResult> ImportMetadataAsync(
        ImportReleaseMetadataRequest request,
        CancellationToken cancellationToken = default);
}
