using DevNexus.Core.Abstractions;
using DevNexus.Core.Services;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Domain.Enums;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Threading.Channels;
using Xunit;

namespace DevNexus.Core.Tests.Services;

/// <summary>
/// 聊天队列调度器测试。
/// </summary>
public sealed class ChatQueueDispatcherTests
{
    /// <summary>
    /// 会话存在待处理交互时，不应继续抢占队列消息。
    /// </summary>
    [Fact]
    public async Task TriggerDispatchAsync_ShouldPauseQueue_WhenPendingInteractionExists()
    {
        var sessionId = Guid.NewGuid();
        var harness = new ScenarioHarness(
            CreateQueuedMessage(sessionId),
            activeInteractions: [CreatePendingInteraction(sessionId)]);

        await harness.Dispatcher.TriggerDispatchAsync(sessionId);

        harness.QueueRepository.DequeueCount.Should().Be(0);
        harness.ChatService.StreamCount.Should().Be(0);
        harness.QueueRepository.UpdatedMessages.Should().BeEmpty();
    }

    /// <summary>
    /// 没有待处理交互时，应正常派发队首消息。
    /// </summary>
    [Fact]
    public async Task TriggerDispatchAsync_ShouldDispatchNextMessage_WhenNoPendingInteractionExists()
    {
        var sessionId = Guid.NewGuid();
        var queuedMessage = CreateQueuedMessage(sessionId);
        var harness = new ScenarioHarness(queuedMessage, activeInteractions: []);

        await harness.Dispatcher.TriggerDispatchAsync(sessionId);

        harness.ChatService.StreamCount.Should().Be(1);
        queuedMessage.Status.Should().Be(QueuedMessageStatus.Completed);
        queuedMessage.ActualMessageId.Should().Be(harness.ChatService.FinalMessageId);
        harness.QueueRepository.UpdatedMessages.Should().ContainSingle().Which.Should().BeSameAs(queuedMessage);
    }

    /// <summary>
    /// 连接已取消时，不应继续抢占队列消息。
    /// </summary>
    [Fact]
    public async Task TriggerDispatchAsync_ShouldNotDequeue_WhenCancellationRequested()
    {
        var sessionId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var harness = new ScenarioHarness(
            CreateQueuedMessage(sessionId),
            activeInteractions: []);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            harness.Dispatcher.TriggerDispatchAsync(sessionId, cts.Token));

        harness.QueueRepository.DequeueCount.Should().Be(0);
        harness.ChatService.StreamCount.Should().Be(0);
        harness.QueueRepository.UpdatedMessages.Should().BeEmpty();
    }

    private static QueuedChatMessage CreateQueuedMessage(Guid sessionId)
    {
        return new QueuedChatMessage
        {
            Id = Guid.NewGuid(),
            ChatSessionId = sessionId,
            UserId = Guid.NewGuid(),
            Content = "排队消息",
            MessageType = ChatConstants.MessageTypeText,
            SequenceNumber = 1,
            Status = QueuedMessageStatus.Pending
        };
    }

    private static PendingInteraction CreatePendingInteraction(Guid sessionId)
    {
        return new PendingInteraction
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Status = PendingInteractionStatus.Pending,
            Title = "等待审批"
        };
    }

    private sealed class ScenarioHarness
    {
        public ScenarioHarness(
            QueuedChatMessage? queuedMessage,
            IReadOnlyList<PendingInteraction> activeInteractions)
        {
            QueueRepository = new RecordingQueuedChatMessageRepository(queuedMessage);
            PendingInteractionRepository = new RecordingPendingInteractionRepository(activeInteractions);
            ChatService = new RecordingChatService();

            var services = new ServiceCollection()
                .AddSingleton<IQueuedChatMessageRepository>(QueueRepository)
                .AddSingleton<IPendingInteractionRepository>(PendingInteractionRepository)
                .AddSingleton<IChatService>(ChatService)
                .BuildServiceProvider();

            Dispatcher = new ChatQueueDispatcher(
                services.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<ChatQueueDispatcher>.Instance);
        }

        public ChatQueueDispatcher Dispatcher { get; }

        public RecordingQueuedChatMessageRepository QueueRepository { get; }

        public RecordingPendingInteractionRepository PendingInteractionRepository { get; }

        public RecordingChatService ChatService { get; }
    }

    private sealed class RecordingQueuedChatMessageRepository : IQueuedChatMessageRepository
    {
        private readonly QueuedChatMessage? _queuedMessage;
        private bool _dequeued;

        public RecordingQueuedChatMessageRepository(QueuedChatMessage? queuedMessage)
        {
            _queuedMessage = queuedMessage;
        }

        public int DequeueCount { get; private set; }

        public List<QueuedChatMessage> UpdatedMessages { get; } = [];

        public Task AddAsync(QueuedChatMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> CancelAllPendingBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<int> CountBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<int> CountPendingBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task DeleteAsync(QueuedChatMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<QueuedChatMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_queuedMessage?.Id == id ? _queuedMessage : null);

        public Task<int> GetMaxSequenceNumberAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<IReadOnlyList<QueuedChatMessage>> ListBySessionAndStatusAsync(
            Guid sessionId,
            QueuedMessageStatus status,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<QueuedChatMessage>>([]);

        public Task<IReadOnlyList<QueuedChatMessage>> ListBySessionAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<QueuedChatMessage>>([]);

        public Task<QueuedChatMessage?> TryDequeueNextAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            DequeueCount++;
            if (_dequeued || _queuedMessage == null || _queuedMessage.ChatSessionId != sessionId)
            {
                return Task.FromResult<QueuedChatMessage?>(null);
            }

            _dequeued = true;
            _queuedMessage.Status = QueuedMessageStatus.Dispatching;
            _queuedMessage.StartedAt = DateTime.UtcNow;
            return Task.FromResult<QueuedChatMessage?>(_queuedMessage);
        }

        public Task UpdateAsync(QueuedChatMessage message, CancellationToken cancellationToken = default)
        {
            UpdatedMessages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingPendingInteractionRepository : IPendingInteractionRepository
    {
        private readonly IReadOnlyList<PendingInteraction> _activeInteractions;

        public RecordingPendingInteractionRepository(IReadOnlyList<PendingInteraction> activeInteractions)
        {
            _activeInteractions = activeInteractions;
        }

        public Task AddAsync(PendingInteraction interaction, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<PendingInteraction>> GetActiveBySessionIdAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PendingInteraction>>(
                _activeInteractions.Where(interaction => interaction.SessionId == sessionId).ToList());
        }

        public Task<PendingInteraction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<PendingInteraction?>(null);

        public Task<IReadOnlyList<PendingInteraction>> GetExpiredPendingAsync(
            DateTime utcNow,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PendingInteraction>>([]);

        public Task UpdateAsync(PendingInteraction interaction, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> UpdateActiveStatusBySessionIdAsync(
            Guid sessionId,
            PendingInteractionStatus fromStatus,
            PendingInteractionStatus toStatus,
            CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class RecordingChatService : IChatService
    {
        public Guid FinalMessageId { get; } = Guid.NewGuid();

        public int StreamCount { get; private set; }

        public Task CancelMessageGenerationAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Guid> CreateChatSessionAsync(Guid userId, string title, CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.NewGuid());

        public Task DeleteChatMessageAsync(Guid messageId, Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> DeleteChatMessagesAsync(
            Guid sessionId,
            List<Guid> messageIds,
            Guid userId,
            CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task DeleteChatSessionAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<List<TerminalRecordDto>> GetActiveTerminalRecordsAsync(Guid sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<TerminalRecordDto>());

        public Task<List<PendingInteractionDto>> GetActivePendingInteractionsAsync(Guid sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<PendingInteractionDto>());

        public Task<List<ChatMessageDto>> GetChatMessagesAsync(Guid sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<ChatMessageDto>());

        public Task<ChatSessionDto?> GetChatSessionAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<ChatSessionDto?>(null);

        public Task<List<ChatSessionDto>> GetChatSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<ChatSessionDto>());

        public Task<TerminalOutputContentDto?> GetTerminalOutputAsync(Guid sessionId, Guid recordId, CancellationToken cancellationToken = default)
            => Task.FromResult<TerminalOutputContentDto?>(null);

        public Task<string> GenerateSmartTitleAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);

        public Task<ChatMessageDto> SaveSystemMessageAsync(
            Guid sessionId,
            string content,
            Guid? relatedMessageId = null,
            string type = ChatConstants.MessageTypeSystem,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ChatMessageDto { Id = Guid.NewGuid(), ChatSessionId = sessionId, Content = content });
        }

        public async Task<ChatMessageDto> StreamMessageAsync(
            ChatRequest chatRequest,
            Guid userId,
            ChannelWriter<BlockDto> blockWriter,
            Func<ChatMessageDto, CancellationToken, Task>? onUserMessageAccepted = null,
            CancellationToken cancellationToken = default)
        {
            StreamCount++;
            var sessionId = chatRequest.SessionId ?? Guid.Empty;
            var content = chatRequest.Content ?? string.Empty;
            var userMessage = new ChatMessageDto
            {
                Id = Guid.NewGuid(),
                ChatSessionId = sessionId,
                SenderId = userId,
                SenderType = ChatConstants.RoleUser,
                Content = content
            };

            if (onUserMessageAccepted != null)
            {
                await onUserMessageAccepted(userMessage, cancellationToken);
            }

            return new ChatMessageDto
            {
                Id = FinalMessageId,
                ChatSessionId = sessionId,
                SenderId = userId,
                SenderType = ChatConstants.RoleAssistant,
                Content = "完成"
            };
        }

        public Task<ChatSessionDto> UpdateChatSessionAsync(
            Guid sessionId,
            Guid userId,
            ChatSessionUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ChatSessionDto { Id = sessionId, Title = request.Title ?? string.Empty });
        }
    }
}
