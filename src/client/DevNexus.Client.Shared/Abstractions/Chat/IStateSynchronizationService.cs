namespace DevNexus.Client.Shared.Abstractions.Chat;

/// <summary>
/// 状态同步服务接口 - 处理状态变化事件和UI更新
/// </summary>
public interface IStateSynchronizationService
{
    /// <summary>
    /// 检查是否有其他会话正在生成（非当前会话）
    /// </summary>
    /// <param name="currentSessionId">当前会话 ID</param>
    bool HasOtherGeneratingSession(Guid currentSessionId);

    /// <summary>
    /// 获取正在生成的其他会话 ID（用于"返回生成中会话"功能）
    /// </summary>
    /// <param name="excludeSessionId">要排除的会话 ID（通常是当前会话）</param>
    /// <returns>正在生成的会话 ID，如果没有则返回 null</returns>
    Guid? GetGeneratingSessionId(Guid excludeSessionId);
}