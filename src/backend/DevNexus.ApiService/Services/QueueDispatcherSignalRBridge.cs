using DevNexus.Core.Services;
using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DevNexus.ApiService.Services;

/// <summary>
/// 队列调度运行时桥接器。
/// 订阅 Core 层 ChatQueueDispatcher 事件，并将队列态、消息块和完成结果转换为前端可消费的统一运行时通道。
/// </summary>
public class QueueDispatcherSignalRBridge : IHostedService
{
    private readonly IHubContext<Hubs.ChatHub> _hubContext;
    private readonly ChatQueueDispatcher _dispatcher;
    private readonly IRuntimeEventNotifier _runtimeEventNotifier;
    private readonly ILogger<QueueDispatcherSignalRBridge> _logger;

    public QueueDispatcherSignalRBridge(
        IHubContext<Hubs.ChatHub> hubContext,
        ChatQueueDispatcher dispatcher,
        IRuntimeEventNotifier runtimeEventNotifier,
        ILogger<QueueDispatcherSignalRBridge> logger)
    {
        _hubContext = hubContext;
        _dispatcher = dispatcher;
        _runtimeEventNotifier = runtimeEventNotifier;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _dispatcher.OnQueuedMessageStarted += HandleQueueDispatchStarted;
        _dispatcher.OnQueuedMessageBlockReceived += HandleQueuedBlockReceived;
        _dispatcher.OnQueuedMessageCompleted += HandleQueueDispatchCompleted;

        _logger.LogInformation("[QueueDispatcherRuntimeBridge] 已订阅队列调度器事件");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _dispatcher.OnQueuedMessageStarted -= HandleQueueDispatchStarted;
        _dispatcher.OnQueuedMessageBlockReceived -= HandleQueuedBlockReceived;
        _dispatcher.OnQueuedMessageCompleted -= HandleQueueDispatchCompleted;

        _logger.LogInformation("[QueueDispatcherRuntimeBridge] 已取消订阅队列调度器事件");
        return Task.CompletedTask;
    }

    private void HandleQueueDispatchStarted(Guid queuedMessageId, Guid sessionId, Guid userId)
    {
        _ = PublishQueueDispatchStartedAsync(queuedMessageId, sessionId, userId);
    }

    private void HandleQueuedBlockReceived(Guid queuedMessageId, Guid sessionId, Guid userId, BlockDto block)
    {
        _ = PublishQueuedMessageBlockAsync(sessionId, userId, block);
    }

    private void HandleQueueDispatchCompleted(
        Guid queuedMessageId,
        Guid sessionId,
        Guid userId,
        ChatMessageDto? finalAiMessage,
        string? errorMessage)
    {
        _ = PublishQueueDispatchCompletionAsync(queuedMessageId, sessionId, userId, finalAiMessage, errorMessage);
    }

    private async Task PublishQueueDispatchStartedAsync(Guid queuedMessageId, Guid sessionId, Guid userId)
    {
        try
        {
            await _runtimeEventNotifier.NotifyAsync(
                userId,
                sessionId,
                ServerEventType.QueueStateChanged,
                new { QueuedMessageId = queuedMessageId, SessionId = sessionId, Action = "started" });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[QueueDispatcherRuntimeBridge] 推送队列启动运行时事件失败");
        }
    }

    private async Task PublishQueuedMessageBlockAsync(Guid sessionId, Guid userId, BlockDto block)
    {
        try
        {
            var userGroup = $"user:{userId}";
            await _hubContext.Clients.Group(userGroup).SendAsync("ReceiveBlock", block);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[QueueDispatcherRuntimeBridge] 推送排队消息流式块失败 | SessionId={SessionId}",
                sessionId);
        }
    }

    private async Task PublishQueueDispatchCompletionAsync(
        Guid queuedMessageId,
        Guid sessionId,
        Guid userId,
        ChatMessageDto? finalAiMessage,
        string? errorMessage)
    {
        try
        {
            var userGroup = $"user:{userId}";

            if (!string.IsNullOrEmpty(errorMessage))
            {
                await _runtimeEventNotifier.NotifyAsync(
                    userId,
                    sessionId,
                    ServerEventType.GenerationFailed,
                    new
                    {
                        SessionId = sessionId,
                        ErrorMessage = "排队消息发送失败: " + errorMessage,
                        ErrorType = "QueueDispatchError"
                    });
                return;
            }

            if (finalAiMessage != null)
            {
                await _hubContext.Clients.Group(userGroup).SendAsync("MessageReceived", finalAiMessage);
            }

            await _runtimeEventNotifier.NotifyAsync(
                userId,
                sessionId,
                ServerEventType.GenerationCompleted,
                new { SessionId = sessionId });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[QueueDispatcherRuntimeBridge] 推送队列完成运行时事件失败");
        }
    }
}
