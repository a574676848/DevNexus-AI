using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// 聊天服务接口
/// </summary>
public interface IChatService
{
    /// <summary>
    /// 创建新的聊天会话
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="title">会话标题</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建的会话ID</returns>
    Task<Guid> CreateChatSessionAsync(
        Guid userId,
        string title,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 发送消息
    /// </summary>
    /// <param name="chatRequest">聊天请求</param>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task SendMessageAsync(
        ChatRequest chatRequest,
        Guid userId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 打断消息生成
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task CancelMessageGenerationAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取聊天会话列表
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>会话列表</returns>
    Task<List<ChatSessionDto>> GetChatSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取聊天消息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>消息列表</returns>
    Task<List<ChatMessageDto>> GetChatMessagesAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 流式发送消息并生成 AI 响应
    /// </summary>
    /// <param name="chatRequest">聊天请求</param>
    /// <param name="userId">用户ID</param>
    /// <param name="onBlockReceived">Block 接收回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task StreamMessageAsync(
        ChatRequest chatRequest,
        Guid userId,
        Func<BlockDto, Task> onBlockReceived,
        CancellationToken cancellationToken = default);
}
