using DevNexus.ApiService.Hubs;
using DevNexus.Domain.Abstractions;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.AspNetCore.SignalR;

namespace DevNexus.ApiService.Services;

/// <summary>
/// 统一运行时事件通知器。
/// 将结构化运行时事件推送到聊天会话所属用户的所有客户端。
/// </summary>
public sealed class RuntimeEventNotifier : IRuntimeEventNotifier
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<RuntimeEventNotifier> _logger;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public RuntimeEventNotifier(
        IHubContext<ChatHub> hubContext,
        ILogger<RuntimeEventNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task NotifyAsync(
        Guid userId,
        Guid sessionId,
        ServerEventType eventType,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        return NotifyAsync(
            userId,
            new ServerEvent
            {
                EventType = eventType,
                SessionId = sessionId,
                Data = data,
                Timestamp = DateTime.UtcNow
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task NotifyAsync(
        Guid userId,
        ServerEvent serverEvent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients.Group($"user:{userId}")
                .SendAsync("ServerEventReceived", serverEvent, cancellationToken);

            _logger.LogDebug(
                "[RuntimeEvent] 已推送结构化运行时事件 | UserId={UserId} SessionId={SessionId} EventType={EventType}",
                userId,
                serverEvent.SessionId,
                serverEvent.EventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[RuntimeEvent] 推送结构化运行时事件失败 | UserId={UserId} SessionId={SessionId} EventType={EventType}",
                userId,
                serverEvent.SessionId,
                serverEvent.EventType);
        }
    }
}
