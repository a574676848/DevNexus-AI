using Microsoft.AspNetCore.SignalR;
using DevNexus.Shared.DTOs.Swarm;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using DevNexus.Domain.Models.Swarm;

namespace DevNexus.ApiService.Hubs;

/// <summary>
/// DevNexus AI Swarm 通用 Hub
/// 提供任务监控、状态推送和交互协商通道
/// Client 可订阅特定 Groups (SessionId) 以获取事件
/// </summary>
public class SwarmHub : Hub
{
    // 单例缓存，用于存储当前活跃的智能体状态（解决新加入客户端看缺失问题）
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, AgentStatusDto>> _activeAgents = new();
    private readonly ILogger<SwarmHub> _logger;
    private readonly DevNexus.Core.Services.Swarm.ISwarmSessionControlService _sessionControlService;
    private readonly DevNexus.Core.Services.Swarm.ISwarmSessionViewService _sessionViewService;
    private readonly DevNexus.Core.Abstractions.IConfirmationService _confirmationService;

    public SwarmHub(
        ILogger<SwarmHub> logger,
        DevNexus.Core.Services.Swarm.ISwarmSessionControlService sessionControlService,
        DevNexus.Core.Services.Swarm.ISwarmSessionViewService sessionViewService,
        DevNexus.Core.Abstractions.IConfirmationService confirmationService)
    {
        _logger = logger;
        _sessionControlService = sessionControlService;
        _sessionViewService = sessionViewService;
        _confirmationService = confirmationService;
    }

    /// <summary>
    /// 处理前端产生的安全确认响应
    /// </summary>
    /// <param name="confirmationId">确认 ID</param>
    /// <param name="approved">是否批准</param>
    public void ResolveConfirmation(string confirmationId, bool approved)
    {
        _logger.LogInformation("Received confirmation response for {Id}: {Approved}", confirmationId, approved);
        _confirmationService.ResolveConfirmation(confirmationId, approved);
    }

    /// <summary>
    /// 加入会话组
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    public async Task JoinSession(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
        _logger.LogInformation("Client {ConnectionId} joined session {SessionId}", Context.ConnectionId, sessionId);

        // 连接后立即推送当前工作包快照，避免客户端晚于会话启动时看不到历史状态。
        var packageSnapshot = await _sessionViewService.GetContextPackageSnapshotAsync(sessionId);
        if (packageSnapshot.PackageCount > 0)
        {
            await Clients.Caller.SendAsync(
                "ServerEventReceived",
                new ServerEvent
                {
                    SessionId = Guid.TryParse(sessionId, out var parsedSessionId) ? parsedSessionId : Guid.Empty,
                    EventType = ServerEventType.SwarmContextPackagesUpdated,
                    Data = packageSnapshot,
                    Timestamp = DateTime.UtcNow
                });
            _logger.LogInformation("Pushed current package snapshot ({Count} packages) to client {ConnectionId} for session {SessionId}",
                packageSnapshot.PackageCount, Context.ConnectionId, sessionId);
        }

        // 5.2 推送当前智能体状态缓存
        if (_activeAgents.TryGetValue(sessionId, out var agents) && agents.Any())
        {
            foreach (var agent in agents.Values)
            {
                await Clients.Caller.SendAsync(
                    "ServerEventReceived",
                    new ServerEvent
                    {
                        SessionId = Guid.TryParse(sessionId, out var parsedSessionId) ? parsedSessionId : Guid.Empty,
                        EventType = ServerEventType.SwarmAgentStatusChanged,
                        Data = new { SessionId = sessionId, agent.Name, agent.Status, agent.CurrentAction },
                        Timestamp = DateTime.UtcNow
                    });
            }
            _logger.LogInformation("Pushed {Count} cached agents to client {ConnectionId}", agents.Count, Context.ConnectionId);
        }
    }

    // 供外部 Service 更新缓存的静态入口
    public static void TrackAgentStatus(string sessionId, string agentName, string status, string currentAction)
    {
        var sessionCache = _activeAgents.GetOrAdd(sessionId, _ => new ConcurrentDictionary<string, AgentStatusDto>());
        sessionCache[agentName] = new AgentStatusDto { Name = agentName, Status = status, CurrentAction = currentAction };
    }

    // 会话结束时清理缓存
    public static void ClearSessionCache(string sessionId)
    {
        _activeAgents.TryRemove(sessionId, out _);
    }

    public async Task PauseSession(string sessionId)
    {
        await _sessionControlService.PauseAsync(sessionId);
        _logger.LogInformation("Session {SessionId} paused by client {ConnectionId}", sessionId, Context.ConnectionId);
    }

    public async Task ResumeSession(string sessionId)
    {
        await _sessionControlService.ResumeAsync(sessionId);
        _logger.LogInformation("Session {SessionId} resumed by client {ConnectionId}", sessionId, Context.ConnectionId);
    }

    public async Task AbortSession(string sessionId)
    {
        await _sessionControlService.AbortAsync(sessionId);
        _logger.LogInformation("Session {SessionId} aborted by client {ConnectionId}", sessionId, Context.ConnectionId);
    }

    /// <summary>
    /// 离开会话组
    /// </summary>
    public async Task LeaveSession(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
        _logger.LogInformation("Client {ConnectionId} left session {SessionId}", Context.ConnectionId, sessionId);
    }

}
