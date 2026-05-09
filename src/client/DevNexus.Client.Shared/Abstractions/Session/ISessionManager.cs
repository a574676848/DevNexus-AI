using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// Session 管理器接口 - 协调 API 请求和本地缓存
/// 实现"在线优先、离线回退"策略
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// 加载会话列表（在线优先，离线回退）
    /// </summary>
    /// <returns>会话列表</returns>
    Task<List<ChatSessionDto>> LoadSessionsAsync();

    /// <summary>
    /// 加载会话消息（在线优先，离线回退）
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>消息列表</returns>
    Task<List<ChatMessageDto>> LoadMessagesAsync(Guid sessionId);

    /// <summary>
    /// 保存会话到缓存
    /// </summary>
    /// <param name="sessions">会话列表</param>
    Task CacheSessionsAsync(IEnumerable<ChatSessionDto> sessions);

    /// <summary>
    /// 保存消息到缓存
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="messages">消息列表</param>
    Task CacheMessagesAsync(Guid sessionId, IEnumerable<ChatMessageDto> messages);

    /// <summary>
    /// 删除会话（同时清理缓存）
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    Task DeleteSessionAsync(Guid sessionId);

    /// <summary>
    /// 清空所有缓存
    /// </summary>
    Task ClearCacheAsync();

    /// <summary>
    /// 创建新会话
    /// </summary>
    /// <param name="title">会话标题</param>
    /// <returns>创建的会话</returns>
    Task<ChatSessionDto?> CreateSessionAsync(string title);

    /// <summary>
    /// 更新会话上下文。
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="request">更新请求</param>
    /// <returns>更新后的会话</returns>
    Task<ChatSessionDto?> UpdateSessionAsync(Guid sessionId, ChatSessionUpdateRequest request);

    /// <summary>
    /// 更新会话标题
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="newTitle">新标题</param>
    Task UpdateSessionTitleAsync(Guid sessionId, string newTitle);

    /// <summary>
    /// 智能生成会话标题
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="currentTitle">当前标题</param>
    /// <returns>生成的标题，如果失败或无变化则返回 null</returns>
    Task<string?> GenerateSmartTitleAsync(Guid sessionId, string currentTitle);

    /// <summary>
    /// 获取是否处于离线模式
    /// </summary>
    bool IsOfflineMode { get; }
}

