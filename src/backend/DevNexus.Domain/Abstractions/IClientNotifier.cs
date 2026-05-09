using DevNexus.Shared.DTOs;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 客户端通知服务接口（用于从后台任务推送消息）
/// </summary>
public interface IClientNotifier
{
    /// <summary>
    /// 通知客户端已生成新消息
    /// </summary>
    Task NotifyMessageGeneratedAsync(Guid userId, Guid sessionId, ChatMessageDto message);

    /// <summary>
    /// 通知客户端图片生成发生错误
    /// </summary>
    Task NotifyImageGenerationErrorAsync(Guid userId, Guid sessionId, string errorMessage);

    /// <summary>
    /// 通知客户端思考/状态提示（通过 ReceiveBlock 推送）
    /// </summary>
    Task NotifyThinkingAsync(
        Guid userId,
        Guid sessionId,
        string content,
        Guid? messageId = null,
        Dictionary<string, object>? metadata = null);
}
