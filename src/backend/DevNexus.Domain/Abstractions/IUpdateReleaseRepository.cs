using DevNexus.Domain.Entities;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 更新发布版本仓储抽象。
/// </summary>
public interface IUpdateReleaseRepository
{
    /// <summary>
    /// 获取全部发布版本及其发布物。
    /// </summary>
    Task<IReadOnlyList<UpdateRelease>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据标识获取发布版本及其发布物。
    /// </summary>
    Task<UpdateRelease?> GetByIdAsync(Guid releaseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存发布版本。
    /// </summary>
    Task<UpdateRelease> SaveAsync(UpdateRelease release, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除发布版本。
    /// </summary>
    Task DeleteAsync(UpdateRelease release, CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存发布物集合。
    /// </summary>
    Task ReplaceArtifactsAsync(Guid releaseId, IReadOnlyCollection<UpdateReleaseArtifact> artifacts, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取同一轨道下最近的已发布版本。
    /// </summary>
    Task<UpdateRelease?> GetPreviousPublishedReleaseAsync(
        string channel,
        Guid excludedReleaseId,
        CancellationToken cancellationToken = default);
}
