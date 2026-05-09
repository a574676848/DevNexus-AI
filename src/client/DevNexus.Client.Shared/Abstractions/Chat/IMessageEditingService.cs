using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Abstractions.Chat;

/// <summary>
/// 消息编辑服务接口 - 处理消息的编辑、重生成和续写操作
/// </summary>
public interface IMessageEditingService
{
    /// <summary>
    /// 处理消息编辑并重新发送。
    /// 删除原始消息及其后所有消息，然后以新内容重新发送。
    /// 返回被删除的消息 ID 列表（供组件同步更新本地消息列表）。
    /// </summary>
    /// <param name="originalMessage">被编辑的原始消息</param>
    /// <param name="newContent">编辑后的新内容</param>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="providerId">Provider ID</param>
    /// <param name="allMessages">当前所有消息列表（用于定位需要删除的后续消息）</param>
    Task<List<Guid>> HandleEditMessageAsync(
        ChatMessageDto originalMessage, string newContent,
        Guid sessionId, Guid? providerId, List<ChatMessageDto> allMessages);

    /// <summary>
    /// 处理重新生成。
    /// 删除目标 Assistant 消息及其前一条 User 消息，然后重新发送用户内容。
    /// 返回被删除的消息 ID 列表和重新发送的用户内容。
    /// </summary>
    /// <param name="message">要重新生成的 Assistant 消息</param>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="providerId">Provider ID</param>
    /// <param name="allMessages">当前所有消息列表（用于查找前一条 User 消息）</param>
    Task<(List<Guid> deletedIds, string? resentContent)> HandleRegenerateAsync(
        ChatMessageDto message, Guid sessionId, Guid? providerId, List<ChatMessageDto> allMessages);

    /// <summary>
    /// 处理截断续写（max_tokens 截断后用户点击"继续生成"）
    /// </summary>
    Task HandleContinueGenerationAsync(BlockDto truncatedBlock, Guid sessionId, Guid? providerId);
}