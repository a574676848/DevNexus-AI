using System.Collections.Concurrent;
using System.Threading.Channels;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Domain.Enums;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevNexus.Core.Services;

/// <summary>
/// 聊天队列调度器实现（Core 层）。
/// 负责原子抢占队列消息、调用聊天服务执行、更新状态。
/// SignalR 推送由上层（API 层）负责。
/// 使用 IServiceScopeFactory 解决 Singleton 依赖 Scoped 服务的问题。
/// </summary>
public class ChatQueueDispatcher : IChatQueueDispatcher
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ChatQueueDispatcher> _logger;

    // 会话级锁：防止同一会话被多个完成事件并发触发
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _sessionLocks = new();

    /// <summary>
    /// 当排队消息开始派发时触发。参数：queuedMessageId, sessionId, userId。
    /// </summary>
    public event Action<Guid, Guid, Guid>? OnQueuedMessageStarted;

    /// <summary>
    /// 当排队消息被持久化为正式用户消息时触发。参数：queuedMessageId, sessionId, userId, userMessage。
    /// </summary>
    public event Action<Guid, Guid, Guid, ChatMessageDto>? OnQueuedUserMessageAccepted;

    /// <summary>
    /// 当排队消息产生流式块时触发。参数：queuedMessageId, sessionId, userId, block。
    /// </summary>
    public event Action<Guid, Guid, Guid, BlockDto>? OnQueuedMessageBlockReceived;

    /// <summary>
    /// 当排队消息派发完成时触发。参数：queuedMessageId, sessionId, userId, finalAiMessage, errorMessage。
    /// </summary>
    public event Action<Guid, Guid, Guid, ChatMessageDto?, string?>? OnQueuedMessageCompleted;

    public ChatQueueDispatcher(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ChatQueueDispatcher> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task TriggerDispatchAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var sessionLock = _sessionLocks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));

        await sessionLock.WaitAsync(cancellationToken);
        try
        {
            await DispatchNextInQueueAsync(sessionId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[ChatQueueDispatcher] 触发队列消费异常 | SessionId={SessionId}", sessionId);
        }
        finally
        {
            sessionLock.Release();
        }
    }

    /// <summary>
    /// 持续消费队列中的所有消息（FIFO 顺序）。
    /// </summary>
    private async Task DispatchNextInQueueAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        while (true)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var queueRepository = scope.ServiceProvider.GetRequiredService<IQueuedChatMessageRepository>();

            var queuedMessage = await queueRepository.TryDequeueNextAsync(sessionId, cancellationToken);

            if (queuedMessage == null)
            {
                return;
            }

            await DispatchSingleMessageAsync(queuedMessage, scope, cancellationToken);
        }
    }

    /// <summary>
    /// 派发单条排队消息到聊天流水线。
    /// </summary>
    private async Task DispatchSingleMessageAsync(
        QueuedChatMessage queuedMessage,
        IServiceScope scope,
        CancellationToken cancellationToken)
    {
        var sessionId = queuedMessage.ChatSessionId;
        var userId = queuedMessage.UserId;

        _logger.LogInformation(
            "[ChatQueueDispatcher] 开始派发排队消息 | QueuedMessageId={QueuedMessageId} SessionId={SessionId} Seq={SequenceNumber}",
            queuedMessage.Id, sessionId, queuedMessage.SequenceNumber);

        OnQueuedMessageStarted?.Invoke(queuedMessage.Id, sessionId, userId);

        var queueRepository = scope.ServiceProvider.GetRequiredService<IQueuedChatMessageRepository>();
        var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();

        // 创建通道用于接收流式输出
        var channel = Channel.CreateBounded<BlockDto>(new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = true
        });

        Exception? consumerException = null;
        var consumerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var block in channel.Reader.ReadAllAsync(cancellationToken))
                {
                    OnQueuedMessageBlockReceived?.Invoke(queuedMessage.Id, sessionId, userId, block);
                }
            }
            catch (Exception ex)
            {
                consumerException = ex;
                _logger.LogWarning(
                    ex,
                    "[ChatQueueDispatcher] 派发排队消息时推送流式块失败 | QueuedMessageId={QueuedMessageId} SessionId={SessionId}",
                    queuedMessage.Id,
                    sessionId);
            }
        }, CancellationToken.None);

        var chatRequest = BuildChatRequest(queuedMessage);

        Exception? producerException = null;
        ChatMessageDto? finalAiMessage = null;

        try
        {
            finalAiMessage = await chatService.StreamMessageAsync(
                chatRequest,
                userId,
                channel.Writer,
                (message, _) =>
                {
                    OnQueuedUserMessageAccepted?.Invoke(queuedMessage.Id, sessionId, userId, message);
                    return Task.CompletedTask;
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            producerException = ex;
            channel.Writer.TryComplete(ex);
        }
        finally
        {
            channel.Writer.TryComplete();
        }

        await consumerTask;

        if (consumerException != null && producerException == null)
        {
            producerException = consumerException;
        }

        await UpdateQueuedMessageStatusAsync(
            queuedMessage, finalAiMessage, producerException, queueRepository, cancellationToken);

        OnQueuedMessageCompleted?.Invoke(
            queuedMessage.Id,
            sessionId,
            userId,
            finalAiMessage,
            producerException?.Message);

        _logger.LogInformation(
            "[ChatQueueDispatcher] 排队消息派发完成 | QueuedMessageId={QueuedMessageId} SessionId={SessionId} Success={Success}",
            queuedMessage.Id, sessionId, producerException == null);
    }

    /// <summary>
    /// 更新排队消息的状态及关联的真实消息 ID。
    /// </summary>
    private static async Task UpdateQueuedMessageStatusAsync(
        QueuedChatMessage queuedMessage,
        ChatMessageDto? finalAiMessage,
        Exception? producerException,
        IQueuedChatMessageRepository queueRepository,
        CancellationToken cancellationToken)
    {
        if (producerException != null)
        {
            queuedMessage.Status = QueuedMessageStatus.Failed;
            queuedMessage.FailureReason = producerException.Message;
            queuedMessage.CompletedAt = DateTime.UtcNow;
        }
        else
        {
            queuedMessage.Status = QueuedMessageStatus.Completed;
            queuedMessage.CompletedAt = DateTime.UtcNow;

            if (finalAiMessage != null)
            {
                queuedMessage.ActualMessageId = finalAiMessage.Id;
            }
        }

        await queueRepository.UpdateAsync(queuedMessage, cancellationToken);
    }

    /// <summary>
    /// 从排队消息构建 ChatRequest。
    /// </summary>
    private static ChatRequest BuildChatRequest(QueuedChatMessage message)
    {
        var request = new ChatRequest
        {
            SessionId = message.ChatSessionId,
            ParentMessageId = message.ParentMessageId,
            Content = message.Content,
            MessageType = message.MessageType,
            SelectedSkillName = message.SelectedSkillName,
            LLMProviderId = message.LLMProviderId,
        };

        if (!string.IsNullOrEmpty(message.ArtifactIdsJson))
        {
            try
            {
                request.ArtifactIds = System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(
                    message.ArtifactIdsJson);
            }
            catch
            {
                // 忽略反序列化失败
            }
        }

        if (!string.IsNullOrEmpty(message.MetadataJson))
        {
            try
            {
                request.Metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                    message.MetadataJson);
            }
            catch
            {
                // 忽略反序列化失败
            }
        }

        return request;
    }
}
