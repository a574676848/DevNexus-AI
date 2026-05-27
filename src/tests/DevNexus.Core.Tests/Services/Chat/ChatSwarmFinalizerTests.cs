using DevNexus.Core.Abstractions;
using DevNexus.Core.Services.Chat;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.DTOs.Swarm;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Threading.Channels;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// Swarm 聊天流收尾块序列测试。
/// </summary>
public sealed class ChatSwarmFinalizerTests
{
    private const string SwarmEventMetadataKey = "swarmEvent";

    [Fact]
    public async Task FinalizeCompletedAsync_ShouldWriteCompletedEventBeforeSingleTerminalBlock()
    {
        var harness = new ScenarioHarness();

        await harness.Finalizer.FinalizeCompletedAsync(
            harness.Message,
            harness.Session,
            "最终结果",
            isTruncated: false,
            thinkingContent: "推理过程",
            harness.Writer);

        var blocks = harness.DrainBlocks();

        blocks.Should().HaveCount(2);
        blocks[0].IsLast.Should().BeFalse();
        blocks[0].Metadata.Should().ContainKey(SwarmEventMetadataKey)
            .WhoseValue.Should().Be(SwarmEventNames.Completed);
        blocks[1].IsLast.Should().BeTrue();
        blocks.Count(block => block.IsLast).Should().Be(1);
        harness.Message.Status.Should().Be(ChatConstants.StatusCompleted);
        harness.Repository.UpdatedMessages.Should().ContainSingle().Which.Should().BeSameAs(harness.Message);
        harness.CompletionCoordinator.CompletedCount.Should().Be(1);
        harness.SwarmEvents.CompletedCount.Should().Be(1);
    }

    [Fact]
    public async Task FinalizeCancelledAsync_ShouldWriteFailedEventBeforeSingleTerminalBlock()
    {
        var harness = new ScenarioHarness();

        await harness.Finalizer.FinalizeCancelledAsync(
            harness.Message,
            harness.Session,
            thinkingContent: string.Empty,
            harness.Writer);

        var blocks = harness.DrainBlocks();

        blocks.Should().HaveCount(2);
        blocks[0].IsLast.Should().BeFalse();
        blocks[0].Metadata.Should().ContainKey(SwarmEventMetadataKey)
            .WhoseValue.Should().Be(SwarmEventNames.Failed);
        blocks[1].IsLast.Should().BeTrue();
        blocks.Count(block => block.IsLast).Should().Be(1);
        harness.Message.Status.Should().Be(ChatConstants.StatusCancelled);
        harness.SwarmEvents.CancelledCount.Should().Be(1);
    }

    [Fact]
    public async Task FinalizeFailedAsync_ShouldWriteWarningBeforeSingleTerminalBlock()
    {
        var harness = new ScenarioHarness();

        await harness.Finalizer.FinalizeFailedAsync(
            harness.Message,
            harness.Session,
            "模型异常",
            harness.Writer);

        var blocks = harness.DrainBlocks();

        blocks.Should().HaveCount(2);
        blocks[0].BlockType.Should().Be(BlockType.Warning);
        blocks[0].IsLast.Should().BeFalse();
        blocks[0].Metadata.Should().ContainKey(SwarmEventMetadataKey)
            .WhoseValue.Should().Be(SwarmEventNames.Failed);
        blocks[1].IsLast.Should().BeTrue();
        blocks.Count(block => block.IsLast).Should().Be(1);
        harness.Message.Status.Should().Be(ChatConstants.StatusError);
        harness.SwarmEvents.FailedCount.Should().Be(1);
    }

    private sealed class ScenarioHarness
    {
        private readonly Channel<BlockDto> _channel = Channel.CreateUnbounded<BlockDto>();

        public ScenarioHarness()
        {
            Repository = new RecordingChatMessageRepository();
            CompletionCoordinator = new RecordingCompletionCoordinator();
            SwarmEvents = new RecordingSwarmEventService();

            var thinkingCoordinator = new ChatThinkingPersistenceCoordinator(
                new EmptyScopeFactory(),
                Repository,
                NullLogger<ChatThinkingPersistenceCoordinator>.Instance);

            Finalizer = new ChatSwarmFinalizer(
                Repository,
                thinkingCoordinator,
                CompletionCoordinator,
                SwarmEvents,
                NullLogger<ChatSwarmFinalizer>.Instance);
        }

        public RecordingChatMessageRepository Repository { get; }

        public RecordingCompletionCoordinator CompletionCoordinator { get; }

        public RecordingSwarmEventService SwarmEvents { get; }

        public ChatSwarmFinalizer Finalizer { get; }

        public ChannelWriter<BlockDto> Writer => _channel.Writer;

        public ChatSession Session { get; } = new()
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        };

        public ChatMessage Message { get; } = new()
        {
            Id = Guid.NewGuid(),
            SenderType = ChatConstants.RoleAssistant
        };

        public IReadOnlyList<BlockDto> DrainBlocks()
        {
            var blocks = new List<BlockDto>();
            while (_channel.Reader.TryRead(out var block))
            {
                blocks.Add(block);
            }

            return blocks;
        }
    }

    private sealed class RecordingChatMessageRepository : IChatMessageRepository
    {
        public List<ChatMessage> UpdatedMessages { get; } = [];

        public Task AddAsync(ChatMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> CountBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task DeleteAsync(ChatMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteRangeAsync(IReadOnlyCollection<ChatMessage> messages, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<ChatMessage?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken = default) => Task.FromResult<ChatMessage?>(null);

        public Task<ChatMessage?> GetByIdWithSessionAsync(Guid messageId, CancellationToken cancellationToken = default) => Task.FromResult<ChatMessage?>(null);

        public Task<ChatMessage?> GetLatestBySessionAndSenderAsync(
            Guid sessionId,
            string senderType,
            CancellationToken cancellationToken = default) => Task.FromResult<ChatMessage?>(null);

        public Task<ChatMessage?> GetLatestBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult<ChatMessage?>(null);

        public Task<Guid?> GetLatestMessageIdBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.FromResult<Guid?>(null);

        public Task<IReadOnlyList<ChatMessage>> ListBySessionAndIdsAsync(
            Guid sessionId,
            IReadOnlyCollection<Guid> messageIds,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ChatMessage>>([]);

        public Task<IReadOnlyList<ChatMessage>> ListBySessionAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ChatMessage>>([]);

        public Task<IReadOnlyList<Guid>> ListIdsBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<IReadOnlyList<ChatMessage>> ListRecentBySessionAsync(
            Guid sessionId,
            int takeCount,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ChatMessage>>([]);

        public Task UpdateAsync(ChatMessage message, CancellationToken cancellationToken = default)
        {
            UpdatedMessages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCompletionCoordinator : IChatMessageCompletionCoordinator
    {
        public int CompletedCount { get; private set; }

        public Task HandleCompletedAsync(
            ChatSession chatSession,
            ChatMessage aiMessage,
            Guid userId,
            int agentLoopAttempt,
            int responseLength,
            bool includeExperienceDistillation = true,
            SelfIterationCandidateDecision? selfIterationCandidate = null,
            CancellationToken cancellationToken = default)
        {
            CompletedCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSwarmEventService : ISwarmEventService
    {
        public int CancelledCount { get; private set; }

        public int CompletedCount { get; private set; }

        public int FailedCount { get; private set; }

        public Task NotifyAgentStatusChangedAsync(
            string sessionId,
            string agentName,
            string status,
            string currentAction,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task NotifyArbitrationEventAsync(
            string sessionId,
            string taskId,
            string eventType,
            string details,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task NotifyConfirmationRequestedAsync(
            string sessionId,
            string confirmationId,
            string operation,
            string payload,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task NotifyContextPackagesUpdatedAsync(
            string sessionId,
            IReadOnlyList<ContextWorkPackageDto> packages,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task NotifyControlCommandAsync(
            string sessionId,
            SwarmControlCommandDto command,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task NotifySessionFinalizedAsync(string sessionId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task NotifySessionStartedAsync(
            string sessionId,
            string description,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task NotifySwarmCancelledAsync(string sessionId, string reason, CancellationToken cancellationToken = default)
        {
            CancelledCount++;
            return Task.CompletedTask;
        }

        public Task NotifySwarmCompletedAsync(string sessionId, int resultLength, CancellationToken cancellationToken = default)
        {
            CompletedCount++;
            return Task.CompletedTask;
        }

        public Task NotifySwarmFailedAsync(string sessionId, string reason, CancellationToken cancellationToken = default)
        {
            FailedCount++;
            return Task.CompletedTask;
        }

        public Task NotifySwarmStartedAsync(string sessionId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task NotifyTaskStatusChangedAsync(
            string sessionId,
            string taskId,
            string status,
            string? message = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class EmptyScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new EmptyScope();
    }

    private sealed class EmptyScope : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new EmptyServiceProvider();

        public void Dispose()
        {
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
