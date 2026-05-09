using DevNexus.Shared.DTOs;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 上下文缓存服务接口
/// </summary>
public interface IContextCacheService
{
    /// <summary>
    /// 获取会话缓存的消息上下文
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>消息列表</returns>
    Task<List<ChatMessageDto>?> GetSessionContextAsync(Guid sessionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 更新会话缓存的消息上下文
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="messages">消息列表（最近20条）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateSessionContextAsync(Guid sessionId, List<ChatMessageDto> messages, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 追加单条消息到缓存
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="message">新消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task AppendMessageAsync(Guid sessionId, ChatMessageDto message, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 清除会话缓存
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ClearSessionContextAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
