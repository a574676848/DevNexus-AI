namespace DevNexus.Core.Abstractions;

/// <summary>
/// 聊天队列调度器。
/// 在长作业（CLI）结束后自动触发队列消费，
/// 保证同一会话串行派发，防止重复触发。
/// </summary>
public interface IChatQueueDispatcher
{
    /// <summary>
    /// 当长作业结束时触发，自动消费队列中的下一条消息。
    /// 该方法内部做并发保护，同一会话不会被重复派发。
    /// </summary>
    /// <param name="sessionId">聊天会话 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task TriggerDispatchAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
