namespace DevNexus.Shared.Enums;

/// <summary>
/// Swarm 上下文工作包状态
/// </summary>
public enum SwarmTaskStatus
{
    Pending,      // 等待中
    Ready,        // 就绪
    InProgress,   // 执行中
    Completed,    // 已完成
    Failed,       // 失败
    Transferred,  // 已流转
    GroupChatting,// 小组讨论中
    Skipped,      // 已跳过
    Evaluating,   // 评估中
    Retrying      // 重试中
}

/// <summary>
/// Swarm 会话全局状态
/// </summary>
public enum SwarmStatus
{
    Running,    // 运行中
    Completed,  // 已完成
    Failed,     // 失败
    Paused,     // 已暂停
    Aborted     // 已终止
}
