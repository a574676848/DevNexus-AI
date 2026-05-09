using DevNexus.Domain.Entities;
using DevNexus.Domain.Enums;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 排队聊天消息仓储接口。
/// 负责排队消息的持久化、状态流转与队列消费相关操作。
/// </summary>
public interface IQueuedChatMessageRepository
{
    /// <summary>
    /// 将排队消息加入数据库（状态为 Pending）。
    /// </summary>
    /// <param name="message">待入队的排队消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AddAsync(QueuedChatMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据 ID 获取排队消息。
    /// </summary>
    /// <param name="id">排队消息 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>排队消息，不存在时返回 null</returns>
    Task<QueuedChatMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定会话下所有排队消息，按 SequenceNumber 升序排列。
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>排队消息列表</returns>
    Task<IReadOnlyList<QueuedChatMessage>> ListBySessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定会话下指定状态的排队消息，按 SequenceNumber 升序排列。
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="status">排队状态</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>排队消息列表</returns>
    Task<IReadOnlyList<QueuedChatMessage>> ListBySessionAndStatusAsync(
        Guid sessionId,
        QueuedMessageStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定会话中状态为 Pending 的第一条消息（FIFO 顺序）。
    /// 该方法在数据库层面保证原子性抢占，防止并发派发。
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>被抢占的排队消息；队列为空时返回 null</returns>
    Task<QueuedChatMessage?> TryDequeueNextAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新排队消息的状态及相关时间戳。
    /// </summary>
    /// <param name="message">排队消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateAsync(QueuedChatMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消指定会话中所有 Pending 状态的排队消息。
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>被取消的消息数量</returns>
    Task<int> CancelAllPendingBySessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定会话的排队消息数量（全部状态）。
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>排队消息数量</returns>
    Task<int> CountBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定会话的 Pending 状态排队消息数量。
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Pending 消息数量</returns>
    Task<int> CountPendingBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除一条排队消息（软删除，依赖 ISoftDelete 全局过滤器）。
    /// </summary>
    /// <param name="message">排队消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task DeleteAsync(QueuedChatMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定会话当前的最大 SequenceNumber。
    /// 返回 0 表示该会话尚无任何排队消息。
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>最大序号</returns>
    Task<int> GetMaxSequenceNumberAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);
}
