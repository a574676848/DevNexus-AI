using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Services;

/// <summary>
/// 投放中心管理服务。
/// </summary>
public interface IUpdateRolloutManagementService
{
    /// <summary>
    /// 获取全部投放规则。
    /// </summary>
    Task<IReadOnlyList<RolloutDto>> GetRolloutsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存投放规则。
    /// </summary>
    Task<RolloutDto> SaveRolloutAsync(SaveRolloutRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 暂停投放。
    /// </summary>
    Task<RolloutDto> PauseAsync(Guid rolloutId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 恢复投放。
    /// </summary>
    Task<RolloutDto> ResumeAsync(Guid rolloutId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 回滚投放到上一个已发布版本。
    /// </summary>
    Task<RolloutDto> RollbackAsync(Guid rolloutId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除投放规则。
    /// </summary>
    Task DeleteAsync(Guid rolloutId, CancellationToken cancellationToken = default);
}
