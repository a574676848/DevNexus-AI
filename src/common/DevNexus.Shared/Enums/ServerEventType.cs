namespace DevNexus.Shared.Enums;

/// <summary>
/// 服务器事件类型枚举，定义了服务器发送给客户端的事件类型
/// </summary>
public enum ServerEventType
{
    /// <summary>
    /// 接收区块数据
    /// </summary>
    ReceiveBlock,

    /// <summary>
    /// 生成开始
    /// </summary>
    GenerationStarted,

    /// <summary>
    /// 生成结束
    /// </summary>
    GenerationCompleted,

    /// <summary>
    /// 生成被打断
    /// </summary>
    GenerationCancelled,

    /// <summary>
    /// 生成失败。
    /// </summary>
    GenerationFailed,

    /// <summary>
    /// CLI 执行已请求。
    /// </summary>
    CliExecRequested,

    /// <summary>
    /// CLI 执行需要审批。
    /// </summary>
    CliExecApprovalRequired,

    /// <summary>
    /// CLI 执行已被拒绝。
    /// </summary>
    CliExecRejected,

    /// <summary>
    /// CLI 执行已启动。
    /// </summary>
    CliExecStarted,

    /// <summary>
    /// CLI 执行输出已更新。
    /// </summary>
    CliExecOutputUpdated,

    /// <summary>
    /// CLI 执行等待输入。
    /// </summary>
    CliExecWaitingForInput,

    /// <summary>
    /// CLI 执行已完成。
    /// </summary>
    CliExecCompleted,

    /// <summary>
    /// CLI 执行失败。
    /// </summary>
    CliExecFailed,

    /// <summary>
    /// CLI 执行已取消。
    /// </summary>
    CliExecCancelled,

    /// <summary>
    /// CLI 执行已超时。
    /// </summary>
    CliExecTimedOut,

    /// <summary>
    /// CLI 执行快照已回滚。
    /// </summary>
    CliExecRolledBack,

    /// <summary>
    /// 队列状态已变更。
    /// </summary>
    QueueStateChanged,

    /// <summary>
    /// 系统通知
    /// </summary>
    SystemNotification,

    /// <summary>
    /// 工具调用开始。
    /// </summary>
    ToolInvocationStarted,

    /// <summary>
    /// 工具调用完成。
    /// </summary>
    ToolInvocationCompleted,

    /// <summary>
    /// 工具调用失败。
    /// </summary>
    ToolInvocationFailed,

    /// <summary>
    /// 工具执行事件批次已更新。
    /// </summary>
    AgentTurnEventsUpdated,

    /// <summary>
    /// 挂起交互已创建。
    /// </summary>
    PendingInteractionCreated,

    /// <summary>
    /// 挂起交互已解决。
    /// </summary>
    PendingInteractionResolved,

    /// <summary>
    /// 挂起交互已过期。
    /// </summary>
    PendingInteractionExpired,

    /// <summary>
    /// 会话已挂起。
    /// </summary>
    SessionSuspended,

    /// <summary>
    /// 会话已恢复。
    /// </summary>
    SessionResumed,

    /// <summary>
    /// 会话已取消。
    /// </summary>
    SessionCancelled,

    /// <summary>
    /// Swarm 会话已启动。
    /// </summary>
    SwarmSessionStarted,

    /// <summary>
    /// Swarm 已启动执行。
    /// </summary>
    SwarmStarted,

    /// <summary>
    /// Swarm 已完成。
    /// </summary>
    SwarmCompleted,

    /// <summary>
    /// Swarm 已失败。
    /// </summary>
    SwarmFailed,

    /// <summary>
    /// Swarm 已取消。
    /// </summary>
    SwarmCancelled,

    /// <summary>
    /// Swarm 工作包快照已更新。
    /// </summary>
    SwarmContextPackagesUpdated,

    /// <summary>
    /// Swarm 智能体状态已更新。
    /// </summary>
    SwarmAgentStatusChanged,

    /// <summary>
    /// Swarm 控制命令事件。
    /// </summary>
    SwarmControlCommand,

    /// <summary>
    /// Swarm 确认请求已创建。
    /// </summary>
    SwarmConfirmationRequested,

    /// <summary>
    /// Swarm 仲裁事件。
    /// </summary>
    SwarmArbitrationEvent
}
