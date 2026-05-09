using DevNexus.Domain.Entities;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 更新投放规则仓储抽象。
/// </summary>
public interface IUpdateRolloutRepository
{
    /// <summary>
    /// 获取全部投放规则。
    /// </summary>
    Task<IReadOnlyList<UpdateRollout>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据标识获取投放规则。
    /// </summary>
    Task<UpdateRollout?> GetByIdAsync(Guid rolloutId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存投放规则。
    /// </summary>
    Task<UpdateRollout> SaveAsync(UpdateRollout rollout, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除投放规则。
    /// </summary>
    Task DeleteAsync(UpdateRollout rollout, CancellationToken cancellationToken = default);

    /// <summary>
    /// 判断发布版本是否仍被投放规则引用。
    /// </summary>
    Task<bool> HasAnyByReleaseIdAsync(Guid releaseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取可参与匹配的候选投放规则。
    /// </summary>
    Task<IReadOnlyList<UpdateRollout>> GetManifestCandidatesAsync(
        string platform,
        string architecture,
        string channel,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}
