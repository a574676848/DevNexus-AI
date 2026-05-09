using DevNexus.Shared.DTOs;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 文件资产服务接口
/// </summary>
public interface IFileAssetService
{
    /// <summary>
    /// 获取单个文件资产
    /// </summary>
    Task<FileAssetDto?> GetFileAssetAsync(
        Guid userId,
        Guid fileAssetId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取会话中的文件资产
    /// </summary>
    Task<IReadOnlyList<FileAssetDto>> GetSessionFileAssetsAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量获取文件资产
    /// </summary>
    Task<IReadOnlyList<FileAssetDto>> GetFileAssetsByIdsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> fileAssetIds,
        CancellationToken cancellationToken = default);
}
