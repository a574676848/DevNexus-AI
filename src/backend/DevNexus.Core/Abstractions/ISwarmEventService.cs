using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevNexus.Shared.DTOs.Swarm;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// Swarm 系统事件服务接口
/// 用于向前端推送状态更新（工作包进度、上下文快照、仲裁结果等）
/// </summary>
public interface ISwarmEventService
{
    /// <summary>
    /// Swarm 会话启动时通知
    /// </summary>
    Task NotifySessionStartedAsync(string sessionId, string description, CancellationToken cancellationToken = default);

    /// <summary>
    /// Swarm 执行开始通知。
    /// </summary>
    Task NotifySwarmStartedAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Swarm 执行完成通知。
    /// </summary>
    Task NotifySwarmCompletedAsync(string sessionId, int resultLength, CancellationToken cancellationToken = default);

    /// <summary>
    /// Swarm 执行失败通知。
    /// </summary>
    Task NotifySwarmFailedAsync(string sessionId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Swarm 执行取消通知。
    /// </summary>
    Task NotifySwarmCancelledAsync(string sessionId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// 任务状态变更通知
    /// </summary>
    Task NotifyTaskStatusChangedAsync(string sessionId, string taskId, string status, string? message = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 上下文工作包快照更新通知。
    /// </summary>
    Task NotifyContextPackagesUpdatedAsync(
        string sessionId,
        IReadOnlyList<ContextWorkPackageDto> packages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 仲裁事件通知（如任务等待、冲突解决）
    /// </summary>
    Task NotifyArbitrationEventAsync(string sessionId, string taskId, string eventType, string details, CancellationToken cancellationToken = default);

    /// <summary>
    /// Agent 状态变更通知
    /// </summary>
    Task NotifyAgentStatusChangedAsync(string sessionId, string agentName, string status, string currentAction, CancellationToken cancellationToken = default);

    /// <summary>
    /// 控制命令通知。
    /// </summary>
    Task NotifyControlCommandAsync(string sessionId, string command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Swarm 会话结束（完成或中止）
    /// </summary>
    Task NotifySessionFinalizedAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 确认请求通知。
    /// </summary>
    Task NotifyConfirmationRequestedAsync(
        string sessionId,
        string confirmationId,
        string operation,
        string payload,
        CancellationToken cancellationToken = default);

}
