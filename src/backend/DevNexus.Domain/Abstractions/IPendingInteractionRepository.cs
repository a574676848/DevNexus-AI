using DevNexus.Domain.Entities;
using DevNexus.Shared.Enums;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 挂起交互仓储接口。
/// </summary>
public interface IPendingInteractionRepository
{
    /// <summary>
    /// 根据标识获取挂起交互。
    /// </summary>
    Task<PendingInteraction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定会话当前活跃的挂起交互列表。
    /// </summary>
    Task<IReadOnlyList<PendingInteraction>> GetActiveBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有已过期但仍处于 Pending 的挂起交互。
    /// </summary>
    Task<IReadOnlyList<PendingInteraction>> GetExpiredPendingAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增挂起交互。
    /// </summary>
    Task AddAsync(PendingInteraction interaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新挂起交互。
    /// </summary>
    Task UpdateAsync(PendingInteraction interaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// 将指定会话当前活跃的挂起交互批量更新为目标状态。
    /// </summary>
    Task<int> UpdateActiveStatusBySessionIdAsync(
        Guid sessionId,
        PendingInteractionStatus fromStatus,
        PendingInteractionStatus toStatus,
        CancellationToken cancellationToken = default);
}
