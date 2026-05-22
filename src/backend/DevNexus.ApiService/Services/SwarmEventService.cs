using DevNexus.Core.Abstractions;
using DevNexus.ApiService.Hubs;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.DTOs.Swarm;
using DevNexus.Shared.Enums;
using Microsoft.AspNetCore.SignalR;
using System.Threading;
using System.Threading.Tasks;

namespace DevNexus.ApiService.Services;

/// <summary>
/// Swarm 实时事件服务实现
/// 通过 HubContext 广播事件到对应 SessionGroup
/// </summary>
public class SwarmEventService : ISwarmEventService
{
    private readonly IHubContext<SwarmHub> _hubContext;
    private readonly DevNexus.Core.Services.Swarm.SwarmSessionRegistry _sessionRegistry;
    private readonly ILogger<SwarmEventService> _logger;

    public SwarmEventService(
        IHubContext<SwarmHub> hubContext,
        DevNexus.Core.Services.Swarm.SwarmSessionRegistry sessionRegistry,
        ILogger<SwarmEventService> logger)
    {
        _hubContext = hubContext;
        _sessionRegistry = sessionRegistry;
        _logger = logger;
    }

    public async Task NotifySessionStartedAsync(string sessionId, string description, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Broadcasting SessionStarted for {id}", sessionId);
        await SendServerEventAsync(
            sessionId,
            ServerEventType.SwarmSessionStarted,
            new { SessionId = sessionId, Description = description },
            cancellationToken);
    }

    public Task NotifySwarmStartedAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return SendServerEventAsync(
            sessionId,
            ServerEventType.SwarmStarted,
            new { SessionId = sessionId, Timestamp = DateTime.UtcNow },
            cancellationToken);
    }

    public Task NotifySwarmCompletedAsync(string sessionId, int resultLength, CancellationToken cancellationToken = default)
    {
        return SendServerEventAsync(
            sessionId,
            ServerEventType.SwarmCompleted,
            new { SessionId = sessionId, ResultLength = resultLength, Timestamp = DateTime.UtcNow },
            cancellationToken);
    }

    public Task NotifySwarmFailedAsync(string sessionId, string reason, CancellationToken cancellationToken = default)
    {
        return SendServerEventAsync(
            sessionId,
            ServerEventType.SwarmFailed,
            new { SessionId = sessionId, Reason = reason, Timestamp = DateTime.UtcNow },
            cancellationToken);
    }

    public Task NotifySwarmCancelledAsync(string sessionId, string reason, CancellationToken cancellationToken = default)
    {
        return SendServerEventAsync(
            sessionId,
            ServerEventType.SwarmCancelled,
            new { SessionId = sessionId, Reason = reason, Timestamp = DateTime.UtcNow },
            cancellationToken);
    }

    public async Task NotifyTaskStatusChangedAsync(string sessionId, string taskId, string status, string? message = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Task {task} status changed to {status}", taskId, status);
        await SendServerEventAsync(
            sessionId,
            ServerEventType.SystemNotification,
            new { SessionId = sessionId, TaskId = taskId, Status = status, Message = message },
            cancellationToken);
    }

    public async Task NotifyContextPackagesUpdatedAsync(
        string sessionId,
        IReadOnlyList<ContextWorkPackageDto> packages,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Broadcasting context package snapshot for {id}", sessionId);
        var isPaused = _sessionRegistry.GetStatus(sessionId) == DevNexus.Core.Services.Swarm.SwarmControlStatus.Paused;
        var statusSummary = DevNexus.Core.Services.Swarm.SwarmSessionStatusSummaryBuilder.Build(packages, isPaused);
        await SendServerEventAsync(
            sessionId,
            ServerEventType.SwarmContextPackagesUpdated,
            new ContextSwarmPackageSnapshotDto
            {
                SessionId = sessionId,
                Packages = packages.ToList(),
                PackageCount = packages.Count,
                StatusSummary = statusSummary
            },
            cancellationToken);
    }

    public async Task NotifyArbitrationEventAsync(string sessionId, string taskId, string eventType, string details, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Broadcasting Arbitration: {event} on {task}", eventType, taskId);
        await SendServerEventAsync(
            sessionId,
            ServerEventType.SwarmArbitrationEvent,
            new { SessionId = sessionId, TaskId = taskId, EventType = eventType, Details = details },
            cancellationToken);
    }

    public async Task NotifyAgentStatusChangedAsync(string sessionId, string agentName, string status, string currentAction, CancellationToken cancellationToken = default)
    {
        // 同步到缓存，以便新连接客户端获取
        SwarmHub.TrackAgentStatus(sessionId, agentName, status, currentAction);

        await SendServerEventAsync(
            sessionId,
            ServerEventType.SwarmAgentStatusChanged,
            new { SessionId = sessionId, AgentName = agentName, Status = status, CurrentAction = currentAction },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task NotifyControlCommandAsync(
        string sessionId,
        SwarmControlCommandDto command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Broadcasting control command {Command} for {SessionId}", command.Command, sessionId);
        await SendServerEventAsync(
            sessionId,
            ServerEventType.SwarmControlCommand,
            command,
            cancellationToken);
    }

    public async Task NotifyConfirmationRequestedAsync(
        string sessionId,
        string confirmationId,
        string operation,
        string payload,
        CancellationToken cancellationToken = default)
    {
        await SendServerEventAsync(
            sessionId,
            ServerEventType.SwarmConfirmationRequested,
            new
            {
                ConfirmationId = confirmationId,
                SessionId = sessionId,
                Operation = operation,
                Payload = payload,
                Timestamp = DateTime.UtcNow
            },
            cancellationToken);
    }

    public async Task NotifySessionFinalizedAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cleaning up session cache for {id}", sessionId);
        SwarmHub.ClearSessionCache(sessionId);
        await Task.CompletedTask;
    }

    private async Task SendServerEventAsync(
        string sessionId,
        ServerEventType eventType,
        object? data,
        CancellationToken cancellationToken)
    {
        await _hubContext.Clients.Group(sessionId).SendAsync(
            "ServerEventReceived",
            new ServerEvent
            {
                SessionId = Guid.TryParse(sessionId, out var parsedSessionId) ? parsedSessionId : Guid.Empty,
                EventType = eventType,
                Data = data,
                Timestamp = DateTime.UtcNow
            },
            cancellationToken);
        _logger.LogDebug("Sent runtime event {EventType} to session {SessionId}", eventType, sessionId);
    }
}
