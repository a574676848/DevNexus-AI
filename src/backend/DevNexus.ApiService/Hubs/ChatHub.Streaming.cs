using System.Threading.Channels;
using DevNexus.ApiService.Services;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DevNexus.ApiService.Hubs;

public partial class ChatHub
{
    /// <summary>
    /// 发送消息
    /// 集成消息排队逻辑：根据会话执行状态决定立即发送、入队排队或转发给运行时。
    /// </summary>
    [Authorize]
    public async Task SendMessage(ChatRequest chatRequest)
    {
        var userId = GetCurrentUserId();
        var userGroup = GetUserGroupName(userId);
        var sessionId = chatRequest.SessionId ?? Guid.Empty;

        SetUserContext(userId, chatRequest.SessionId);

        // 第一步：通过排队服务决定消息流向
        var enqueueResult = await _chatQueueService.HandleSendRequestAsync(
            userId,
            sessionId,
            chatRequest.Content,
            chatRequest.ParentMessageId,
            chatRequest.MessageType,
            chatRequest.SelectedSkillName,
            chatRequest.ArtifactIds,
            chatRequest.LLMProviderId,
            chatRequest.Metadata,
            Context.ConnectionAborted);

        switch (enqueueResult.Decision)
        {
            case DevNexus.Domain.Enums.ChatExecutionDecision.Immediate:
                // 立即发送：走原有流式发送链路
                await ExecuteStreamMessageAsync(chatRequest, userId, sessionId, userGroup);
                break;

            case DevNexus.Domain.Enums.ChatExecutionDecision.Queued:
                // 已入队：通知客户端排队成功
                _logger.LogInformation(
                    "[SignalR.Chat] 消息已入队 | SessionId={SessionId} UserId={UserId} QueuedMessageId={QueuedMessageId} PendingCount={PendingCount}",
                    sessionId, userId, enqueueResult.QueuedMessageId, enqueueResult.PendingCount);
                await _runtimeEventNotifier.NotifyAsync(
                    userId,
                    sessionId,
                    ServerEventType.QueueStateChanged,
                    new
                    {
                        QueuedMessageId = enqueueResult.QueuedMessageId,
                        SessionId = sessionId,
                        PendingCount = enqueueResult.PendingCount,
                        Message = enqueueResult.Message,
                        Action = "accepted"
                    },
                    Context.ConnectionAborted);
                break;

            case DevNexus.Domain.Enums.ChatExecutionDecision.ForwardToRuntimeInput:
                // 等待输入态：将输入直接发送给当前运行时
                await ForwardToRuntimeInputAsync(userId, sessionId, userGroup, chatRequest.Content);
                break;

            default:
                // 拒绝：发送错误通知
                await _runtimeEventNotifier.NotifyAsync(
                    userId,
                    sessionId,
                    ServerEventType.GenerationFailed,
                    new
                    {
                        SessionId = sessionId,
                        ErrorMessage = enqueueResult.Message ?? "当前无法处理发送请求",
                        ErrorType = "Rejected"
                    },
                    Context.ConnectionAborted);
                break;
        }
    }

    /// <summary>
    /// 执行流式消息发送（原有链路）。
    /// </summary>
    private async Task ExecuteStreamMessageAsync(
        ChatRequest chatRequest,
        Guid userId,
        Guid sessionId,
        string userGroup)
    {
        await _runtimeEventNotifier.NotifyAsync(
            userId,
            sessionId,
            ServerEventType.GenerationStarted,
            new
            {
                SessionId = sessionId,
                chatRequest.ParentMessageId,
                chatRequest.MessageType
            },
            Context.ConnectionAborted);

        var channel = Channel.CreateBounded<BlockDto>(new BoundedChannelOptions(128)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = true
        });

        ChatMessageDto? finalAiMessage = null;
        Exception? producerException = null;
        Exception? consumerException = null;

        var producerTask = Task.Run(async () =>
        {
            try
            {
                finalAiMessage = await _chatService.StreamMessageAsync(
                    chatRequest,
                    userId,
                    channel.Writer,
                    Context.ConnectionAborted);
            }
            catch (Exception ex)
            {
                producerException = ex;
                channel.Writer.TryComplete(ex);
            }
        });

        try
        {
            await PublishBlocksAsync(userGroup, sessionId, userId, channel.Reader, channel.Writer, ex => consumerException = ex);
        }
        finally
        {
            await producerTask;
        }

        try
        {
            await PublishStreamingCompletionAsync(
                userGroup,
                sessionId,
                userId,
                finalAiMessage,
                producerException,
                consumerException);
        }
        finally
        {
            await ClearUserContextAndRefreshSessionsAsync(userId, userGroup);
        }
    }

    /// <summary>
    /// 将输入转发给当前运行中的 CLI 会话。
    /// </summary>
    private async Task ForwardToRuntimeInputAsync(Guid userId, Guid sessionId, string userGroup, string content)
    {
        var session = await _cliRuntimeCoordinator.WriteInputAsync(userId, sessionId, content, Context.ConnectionAborted);

        _logger.LogDebug(
            "[SignalR.Chat] 输入已转发给 CLI 运行时 | SessionId={SessionId}", sessionId);
    }

    private void SetUserContext(Guid userId, Guid? sessionId)
    {
        _userContextAccessor.CurrentUserId = userId;
        _userContextAccessor.CurrentSessionId = sessionId?.ToString();
        _userContextAccessor.CurrentConnectionId = Context.ConnectionId;
    }

    private async Task PublishBlocksAsync(
        string userGroup,
        Guid sessionId,
        Guid userId,
        ChannelReader<BlockDto> reader,
        ChannelWriter<BlockDto> writer,
        Action<Exception> onConsumerException)
    {
        try
        {
            await foreach (var block in reader.ReadAllAsync(Context.ConnectionAborted))
            {
                await Clients.Group(userGroup).SendAsync(
                    "ReceiveBlock",
                    block,
                    Context.ConnectionAborted);
            }
        }
        catch (ChannelClosedException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            onConsumerException(ex);
            writer.TryComplete(ex);

            _logger.LogError(
                ex,
                "[SignalR.Chat] Consumer publish failed (e.g. Redis timeout) | UserId={UserId} SessionId={SessionId}",
                userId,
                sessionId);
        }
    }

    private async Task PublishStreamingCompletionAsync(
        string userGroup,
        Guid sessionId,
        Guid userId,
        ChatMessageDto? finalAiMessage,
        Exception? producerException,
        Exception? consumerException)
    {
        if (consumerException != null)
        {
            await HandleConsumerFailureAsync(userGroup, sessionId, userId, finalAiMessage, producerException, consumerException);
            return;
        }

        if (producerException is OperationCanceledException)
        {
            await HandleProducerCancellationAsync(userGroup, sessionId, userId);
            return;
        }

        if (producerException != null)
        {
            _logger.LogError(
                producerException,
                "[SignalR.Chat] StreamMessage error | UserId={UserId} SessionId={SessionId}",
                userId,
                sessionId);

            await _runtimeEventNotifier.NotifyAsync(
                userId,
                sessionId,
                ServerEventType.GenerationFailed,
                new
                {
                    SessionId = sessionId,
                    ErrorMessage = producerException.Message,
                    ErrorType = producerException.GetType().Name
                },
                CancellationToken.None);
            return;
        }

        await Clients.Group(userGroup).SendAsync(
            "MessageReceived",
            finalAiMessage,
            CancellationToken.None);

        await _runtimeEventNotifier.NotifyAsync(
            userId,
            sessionId,
            ServerEventType.GenerationCompleted,
            new { SessionId = sessionId },
            CancellationToken.None);

        // 生成完成后触发队列消费（仅在没有异常的情况下）
        _ = TriggerQueueDispatchAsync(sessionId);
    }

    private async Task HandleConsumerFailureAsync(
        string userGroup,
        Guid sessionId,
        Guid userId,
        ChatMessageDto? finalAiMessage,
        Exception? producerException,
        Exception consumerException)
    {
        _logger.LogError(
            consumerException,
            "[SignalR.Chat] Block publish infrastructure error | UserId={UserId} SessionId={SessionId}",
            userId,
            sessionId);

        try
        {
            await _runtimeEventNotifier.NotifyAsync(
                userId,
                sessionId,
                ServerEventType.GenerationFailed,
                new
                {
                    SessionId = sessionId,
                    ErrorMessage = "消息推送异常，请刷新页面查看最新内容",
                    ErrorType = consumerException.GetType().Name
                },
                CancellationToken.None);
        }
        catch (Exception notifyEx)
        {
            _logger.LogWarning(notifyEx, "[SignalR.Chat] Failed to send runtime failure event after consumer failure");
        }

        if (producerException != null || finalAiMessage == null)
        {
            return;
        }

        try
        {
            await Clients.Group(userGroup).SendAsync("MessageReceived", finalAiMessage, CancellationToken.None);
            await _runtimeEventNotifier.NotifyAsync(
                userId,
                sessionId,
                ServerEventType.GenerationCompleted,
                new { SessionId = sessionId },
                CancellationToken.None);
        }
        catch (Exception recoveryEx)
        {
            _logger.LogWarning(recoveryEx, "[SignalR.Chat] Recovery send also failed, client should refresh");
        }
    }

    private async Task HandleProducerCancellationAsync(string userGroup, Guid sessionId, Guid userId)
    {
        var isConnectionAborted = Context.ConnectionAborted.IsCancellationRequested;

        _logger.LogDebug(
            "[SignalR.Chat] Generation cancelled | UserId={UserId} SessionId={SessionId} ConnectionAborted={ConnectionAborted}",
            userId,
            sessionId,
            isConnectionAborted);

        if (isConnectionAborted)
        {
            return;
        }

        try
        {
            await _runtimeEventNotifier.NotifyAsync(
                userId,
                sessionId,
                ServerEventType.GenerationCancelled,
                new { SessionId = sessionId },
                CancellationToken.None);
        }
        catch (Exception notifyEx)
        {
            _logger.LogWarning(
                notifyEx,
                "[SignalR.Chat] Failed to broadcast GenerationCancelled runtime event | UserId={UserId} SessionId={SessionId}",
                userId,
                sessionId);
        }

        // 取消后也触发队列消费，让排队消息有机会被调度
        _ = TriggerQueueDispatchAsync(sessionId);
    }

    /// <summary>
    /// 触发队列消费（fire-and-forget，不阻塞当前请求）。
    /// </summary>
    private async Task TriggerQueueDispatchAsync(Guid sessionId)
    {
        try
        {
            await _chatQueueDispatcher.TriggerDispatchAsync(sessionId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // 队列调度失败不影响主流程，仅记录日志
            _logger.LogWarning(ex,
                "[SignalR.Chat] 队列调度触发异常 | SessionId={SessionId}", sessionId);
        }
    }

    private async Task ClearUserContextAndRefreshSessionsAsync(Guid userId, string userGroup)
    {
        _userContextAccessor.CurrentSessionId = null;
        _userContextAccessor.CurrentUserId = null;
        _userContextAccessor.CurrentConnectionId = null;

        try
        {
            var sessions = await _chatService.GetChatSessionsAsync(userId, CancellationToken.None);
            await Clients.Group(userGroup).SendAsync(
                "ChatSessionsReceived",
                sessions,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SignalR.Chat] Failed to broadcast sessions update");
        }
    }
}
