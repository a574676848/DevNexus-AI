using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// 聊天消息排队服务。
/// 负责接管"发送请求"的第一入口，统一返回"立即发送"或"已入队"等决策结果，
/// 并提供查看队列、取消队列项、清空队列等能力。
/// </summary>
public interface IChatQueueService
{
    /// <summary>
    /// 处理发送请求：根据当前会话执行状态决定立即发送、入队排队或转发给运行时。
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="content">消息内容</param>
    /// <param name="parentMessageId">父消息 ID</param>
    /// <param name="messageType">消息类型</param>
    /// <param name="selectedSkillName">选中的 Skill 名称</param>
    /// <param name="artifactIds">关联的 Artifact ID 列表</param>
    /// <param name="llmProviderId">LLM Provider ID</param>
    /// <param name="metadata">附加元数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>排队处理结果</returns>
    Task<EnqueueResult> HandleSendRequestAsync(
        Guid userId,
        Guid sessionId,
        string content,
        Guid? parentMessageId,
        string messageType = ChatConstants.MessageTypeText,
        string? selectedSkillName = null,
        IReadOnlyCollection<Guid>? artifactIds = null,
        Guid? llmProviderId = null,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定会话的排队消息列表。
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>排队消息列表</returns>
    Task<IReadOnlyList<QueuedChatMessageDto>> GetQueueAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消一条排队消息（仅 Pending 状态可取消）。
    /// </summary>
    /// <param name="queuedMessageId">排队消息 ID</param>
    /// <param name="userId">操作用户 ID（用于权限校验）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否取消成功</returns>
    Task<bool> CancelQueuedMessageAsync(
        Guid sessionId,
        Guid queuedMessageId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 清空指定会话中所有 Pending 状态的排队消息。
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>被取消的消息数量</returns>
    Task<int> ClearQueueAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定会话的 Pending 状态排队消息数量。
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Pending 消息数量</returns>
    Task<int> GetPendingCountAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);
}
