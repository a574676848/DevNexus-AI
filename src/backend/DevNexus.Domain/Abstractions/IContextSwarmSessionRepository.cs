using DevNexus.Domain.Entities;
using DevNexus.Shared.Enums;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 上下文驱动 Swarm 会话仓储接口。
/// </summary>
public interface IContextSwarmSessionRepository
{
    /// <summary>
    /// 根据外部会话 ID 获取会话实体。
    /// </summary>
    Task<ContextSwarmSession?> GetBySessionIdAsync(string sessionId);

    /// <summary>
    /// 保存或更新会话。
    /// </summary>
    Task SaveAsync(ContextSwarmSession session);

    /// <summary>
    /// 更新工作包记录。
    /// </summary>
    Task UpdateTaskAsync(string sessionId, ContextWorkPackageRecord task);

    /// <summary>
    /// 获取用户的会话列表。
    /// </summary>
    Task<List<ContextSwarmSession>> GetUserSessionsAsync(Guid userId);

    /// <summary>
    /// 获取关联指定外部会话 ID 的会话。
    /// </summary>
    Task<List<ContextSwarmSession>> ListByExternalSessionIdAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量删除会话。
    /// </summary>
    Task DeleteRangeAsync(IReadOnlyCollection<ContextSwarmSession> sessions, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新会话状态。
    /// </summary>
    Task UpdateSessionStatusAsync(string sessionId, SwarmStatus status, string? result = null);

    /// <summary>
    /// 获取所有中断的会话。
    /// </summary>
    Task<List<ContextSwarmSession>> GetInterruptedSessionsAsync();
}
