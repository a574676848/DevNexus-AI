using DevNexus.Domain.Entities;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 聊天消息仓储接口。
/// </summary>
public interface IChatMessageRepository
{
    /// <summary>
    /// 获取会话下全部消息。
    /// </summary>
    Task<IReadOnlyList<ChatMessage>> ListBySessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据消息 ID 获取消息。
    /// </summary>
    Task<ChatMessage?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取会话下最近的消息列表。
    /// </summary>
    Task<IReadOnlyList<ChatMessage>> ListRecentBySessionAsync(
        Guid sessionId,
        int takeCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取会话下全部消息 ID。
    /// </summary>
    Task<IReadOnlyList<Guid>> ListIdsBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据消息 ID 获取消息及其会话。
    /// </summary>
    Task<ChatMessage?> GetByIdWithSessionAsync(Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取会话内指定消息集合。
    /// </summary>
    Task<IReadOnlyList<ChatMessage>> ListBySessionAndIdsAsync(
        Guid sessionId,
        IReadOnlyCollection<Guid> messageIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取会话下最新一条消息。
    /// </summary>
    Task<ChatMessage?> GetLatestBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取会话下指定发送者的最新消息。
    /// </summary>
    Task<ChatMessage?> GetLatestBySessionAndSenderAsync(
        Guid sessionId,
        string senderType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取会话消息数量。
    /// </summary>
    Task<int> CountBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取会话下最新消息 ID。
    /// </summary>
    Task<Guid?> GetLatestMessageIdBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增消息。
    /// </summary>
    Task AddAsync(ChatMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新消息。
    /// </summary>
    Task UpdateAsync(ChatMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除消息。
    /// </summary>
    Task DeleteAsync(ChatMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量删除消息。
    /// </summary>
    Task DeleteRangeAsync(IReadOnlyCollection<ChatMessage> messages, CancellationToken cancellationToken = default);
}
