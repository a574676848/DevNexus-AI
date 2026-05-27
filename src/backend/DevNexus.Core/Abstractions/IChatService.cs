using System.Threading.Channels;
using DevNexus.Shared.Constants;
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
    /// 获取指定会话当前活跃的终端记录。
    /// </summary>
    Task<List<TerminalRecordDto>> GetActiveTerminalRecordsAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定会话当前活跃的挂起交互。
    /// </summary>
    Task<List<PendingInteractionDto>> GetActivePendingInteractionsAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定终端记录的完整输出内容。
    /// </summary>
    Task<TerminalOutputContentDto?> GetTerminalOutputAsync(
        Guid sessionId,
        Guid recordId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除聊天消息
    /// </summary>
    /// <param name="messageId">消息ID</param>
    /// <param name="userId">用户ID（用于验证所有权）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task DeleteChatMessageAsync(
        Guid messageId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量删除聊天消息（用于编辑/重新发送场景，提升性能）
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="messageIds">要删除的消息ID列表</param>
    /// <param name="userId">用户ID（用于验证所有权）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>成功删除的消息数量</returns>
    Task<int> DeleteChatMessagesAsync(
        Guid sessionId,
        List<Guid> messageIds,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除聊天会话
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="userId">用户ID（用于验证所有权）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务</returns>
    Task DeleteChatSessionAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新聊天会话信息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="userId">用户ID（用于验证所有权）</param>
    /// <param name="request">更新请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的会话 DTO</returns>
    Task<ChatSessionDto> UpdateChatSessionAsync(
        Guid sessionId,
        Guid userId,
        ChatSessionUpdateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 流式发送消息并生成 AI 响应
    /// 使用 Channel 解耦 LLM 消费与下游推送，防止反压阻塞 LLM
    /// </summary>
    /// <param name="chatRequest">聊天请求</param>
    /// <param name="userId">用户ID</param>
    /// <param name="blockWriter">Block 输出通道（由调用方创建 Channel 并传入 Writer）</param>
    /// <param name="onUserMessageAccepted">用户消息持久化后的回调，用于上层立即回推正式消息</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>最终 AI 消息 DTO</returns>
    Task<ChatMessageDto> StreamMessageAsync(
        ChatRequest chatRequest,
        Guid userId,
        ChannelWriter<BlockDto> blockWriter,
        Func<ChatMessageDto, CancellationToken, Task>? onUserMessageAccepted = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用 LLM 智能生成会话标题
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="userId">用户ID（用于验证所有权）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>生成的智能标题</returns>
    Task<string> GenerateSmartTitleAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取单个聊天会话详情
    /// </summary>
    Task<ChatSessionDto?> GetChatSessionAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存系统/工具生成的消息
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="content">消息内容</param>
    /// <param name="relatedMessageId">关联的消息ID（可选）</param>
    /// <param name="type">消息类型（system/tool）</param>
    /// <returns>保存的消息DTO</returns>
    Task<ChatMessageDto> SaveSystemMessageAsync(
        Guid sessionId,
        string content,
        Guid? relatedMessageId = null,
        string type = ChatConstants.MessageTypeSystem,
        CancellationToken cancellationToken = default);

    /// <summary>
}
