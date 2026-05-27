using DevNexus.Core.Abstractions;
using DevNexus.Core.Services.Chat;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 挂起交互过期收口协调器测试。
/// </summary>
public sealed class PendingInteractionExpirationCoordinatorTests
{
    [Fact]
    public async Task ExpireAsync_ShouldExpireNotifyAndResumeQueueOncePerSession()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var utcNow = new DateTime(2026, 5, 27, 9, 0, 0, DateTimeKind.Utc);
        var interactions = new[]
        {
            CreateInteraction(sessionId, userId, "审批一"),
            CreateInteraction(sessionId, userId, "审批二")
        };
        var repository = new RecordingPendingInteractionRepository(interactions);
        var notifier = new RecordingRuntimeEventNotifier();
        var dispatcher = new RecordingChatQueueDispatcher();
        var coordinator = CreateCoordinator(repository, notifier, dispatcher);

        await coordinator.ExpireAsync(utcNow);

        repository.UpdatedInteractions.Should().HaveCount(2);
        repository.UpdatedInteractions.Should().OnlyContain(item =>
            item.Status == PendingInteractionStatus.Expired && item.UpdatedAt == utcNow);
        notifier.Events.Should().HaveCount(2);
        notifier.Events.Should().OnlyContain(item =>
            item.UserId == userId
            && item.SessionId == sessionId
            && item.EventType == ServerEventType.PendingInteractionExpired);
        dispatcher.DispatchedSessions.Should().ContainSingle().Which.Should().Be(sessionId);
    }

    [Fact]
    public async Task ExpireAsync_ShouldNotNotifyOrDispatch_WhenNoExpiredInteractionExists()
    {
        var repository = new RecordingPendingInteractionRepository([]);
        var notifier = new RecordingRuntimeEventNotifier();
        var dispatcher = new RecordingChatQueueDispatcher();
        var coordinator = CreateCoordinator(repository, notifier, dispatcher);

        await coordinator.ExpireAsync(DateTime.UtcNow);

        repository.UpdatedInteractions.Should().BeEmpty();
        notifier.Events.Should().BeEmpty();
        dispatcher.DispatchedSessions.Should().BeEmpty();
    }

    private static PendingInteractionExpirationCoordinator CreateCoordinator(
        IPendingInteractionRepository repository,
        IRuntimeEventNotifier notifier,
        IChatQueueDispatcher dispatcher)
    {
        return new PendingInteractionExpirationCoordinator(
            repository,
            notifier,
            dispatcher,
            NullLogger<PendingInteractionExpirationCoordinator>.Instance);
    }

    private static PendingInteraction CreateInteraction(Guid sessionId, Guid userId, string title)
    {
        return new PendingInteraction
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            ChatSession = new ChatSession
            {
                Id = sessionId,
                UserId = userId
            },
            Status = PendingInteractionStatus.Pending,
            Title = title
        };
    }

    private sealed class RecordingPendingInteractionRepository : IPendingInteractionRepository
    {
        private readonly IReadOnlyList<PendingInteraction> _expiredInteractions;

        public RecordingPendingInteractionRepository(IReadOnlyList<PendingInteraction> expiredInteractions)
        {
            _expiredInteractions = expiredInteractions;
        }

        public List<PendingInteraction> UpdatedInteractions { get; } = [];

        public Task AddAsync(PendingInteraction interaction, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<PendingInteraction>> GetActiveBySessionIdAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PendingInteraction>>([]);

        public Task<PendingInteraction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<PendingInteraction?>(null);

        public Task<IReadOnlyList<PendingInteraction>> GetExpiredPendingAsync(
            DateTime utcNow,
            CancellationToken cancellationToken = default) => Task.FromResult(_expiredInteractions);

        public Task UpdateAsync(PendingInteraction interaction, CancellationToken cancellationToken = default)
        {
            UpdatedInteractions.Add(interaction);
            return Task.CompletedTask;
        }

        public Task<int> UpdateActiveStatusBySessionIdAsync(
            Guid sessionId,
            PendingInteractionStatus fromStatus,
            PendingInteractionStatus toStatus,
            CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class RecordingRuntimeEventNotifier : IRuntimeEventNotifier
    {
        public List<RuntimeEventRecord> Events { get; } = [];

        public Task NotifyAsync(
            Guid userId,
            Guid sessionId,
            ServerEventType eventType,
            object? data = null,
            CancellationToken cancellationToken = default)
        {
            Events.Add(new RuntimeEventRecord(userId, sessionId, eventType));
            return Task.CompletedTask;
        }

        public Task NotifyAsync(Guid userId, ServerEvent serverEvent, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class RecordingChatQueueDispatcher : IChatQueueDispatcher
    {
        public List<Guid> DispatchedSessions { get; } = [];

        public Task TriggerDispatchAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            DispatchedSessions.Add(sessionId);
            return Task.CompletedTask;
        }
    }

    private readonly record struct RuntimeEventRecord(
        Guid UserId,
        Guid SessionId,
        ServerEventType EventType);
}
