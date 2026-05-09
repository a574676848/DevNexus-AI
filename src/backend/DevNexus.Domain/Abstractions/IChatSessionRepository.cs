using DevNexus.Domain.Entities;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 聊天会话仓储接口。
/// </summary>
public interface IChatSessionRepository
{
    /// <summary>
    /// 根据会话 ID 获取聊天会话。
    /// </summary>
    Task<ChatSession?> GetByIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据用户和会话 ID 获取聊天会话。
    /// </summary>
    Task<ChatSession?> GetByIdAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户全部会话。
    /// </summary>
    Task<IReadOnlyList<ChatSession>> ListByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增聊天会话。
    /// </summary>
    Task AddAsync(ChatSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新聊天会话。
    /// </summary>
    Task UpdateAsync(ChatSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除聊天会话。
    /// </summary>
    Task DeleteAsync(ChatSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据会话 ID 获取所属用户 ID。
    /// </summary>
    Task<Guid?> GetUserIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
