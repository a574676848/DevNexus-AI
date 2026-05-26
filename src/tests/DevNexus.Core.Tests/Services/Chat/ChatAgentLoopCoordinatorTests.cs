using DevNexus.Core.Abstractions;
using DevNexus.Core.Abstractions.Observability;
using DevNexus.Core.Models.Evaluation;
using DevNexus.Core.Services.Chat;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Threading.Channels;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// Agent Loop 协调器真实恢复路径测试。
/// </summary>
public sealed class ChatAgentLoopCoordinatorTests
{
    /// <summary>
    /// CLI 命令仍在运行时，协调器应生成内部续接消息并发布工具事件。
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldCreateRetryMessage_WhenCliCommandNeedsContinuation()
    {
        var harness = new ScenarioHarness();

        var decision = await harness.HandleAsync(
            [
                CreateFailedRecord(
                    "HostService.ExecuteCommandAsync",
                    ToolSuggestedAction.WaitForCompletion,
                    "命令仍在运行")
            ],
            agentLoopAttempt: 0);

        decision.Action.Should().Be(AgentLoopAction.Retry);
        decision.RepairMessage.Should().NotBeNull();
        decision.RepairMessage!.Metadata.Should().ContainKey("internalRepairPrompt")
            .WhoseValue.Should().Be(true);
        decision.RepairMessage.Content["text"].Should().BeOfType<string>()
            .Which.Should().Contain("HostService.WaitCommandAsync");
        harness.Messages.Should().ContainSingle().Which.Should().BeSameAs(decision.RepairMessage);
        harness.EventTypes.Should().Contain(ServerEventType.AgentTurnEventsUpdated);
        harness.Metrics.RepairAttempts.Should().Equal(true);
        harness.TryReadBlock(out _).Should().BeFalse();
    }

    /// <summary>
    /// 同一终端停止动作反复未闭环时，协调器应停止自动修复并写出低噪告警。
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldStopWithoutRetryMessage_WhenStopContinuationBudgetIsExceeded()
    {
        var harness = new ScenarioHarness();

        var decision = await harness.HandleAsync(
            [
                CreateFailedRecord(
                    "HostService.StopCommandAsync",
                    ToolSuggestedAction.StopCommand,
                    "停止请求未完成")
            ],
            agentLoopAttempt: 2);

        decision.Action.Should().Be(AgentLoopAction.Stop);
        decision.RepairMessage.Should().BeNull();
        harness.Messages.Should().BeEmpty();
        harness.EventTypes.Should().Contain(ServerEventType.AgentTurnEventsUpdated);
        harness.Metrics.RepairAttempts.Should().BeEmpty();
        harness.TryReadBlock(out var warning).Should().BeTrue();
        warning!.Content.Should().Contain("已多次尝试停止同一终端会话");
    }

    /// <summary>
    /// 高轮次不应因为固定上限提前停止，只要恢复链仍判定可修复，就应继续生成修复消息。
    /// </summary>
    [Fact]
    public async Task HandleAsync_ShouldKeepRetrying_WhenAttemptIsHighButRecoveryStillAllowsRepair()
    {
        var harness = new ScenarioHarness(
            new FailingEvaluator(),
            new FailingEvaluator(),
            new FixedRepairContextBuilder());

        var decision = await harness.HandleAsync(
            [
                CreateFailedRecord(
                    "WebSearchPlugin.ReadWebpage",
                    ToolSuggestedAction.None,
                    "网页读取被策略拒绝")
            ],
            agentLoopAttempt: 99);

        decision.Action.Should().Be(AgentLoopAction.Retry);
        decision.RepairMessage.Should().NotBeNull();
        harness.Metrics.RepairAttempts.Should().ContainSingle().Which.Should().BeTrue();
        harness.TryReadBlock(out _).Should().BeFalse();
    }

    private static ToolExecutionRecord CreateFailedRecord(
        string toolName,
        ToolSuggestedAction suggestedAction,
        string errorSummary)
    {
        return new ToolExecutionRecord
        {
            ToolCallId = Guid.NewGuid(),
            ToolName = toolName,
            Arguments = """{"sessionId":"current"}""",
            Success = false,
            Retryable = true,
            RequiresHumanIntervention = false,
            SuggestedAction = suggestedAction,
            FailureReason = ToolFailureReason.FatalExecutionError,
            ErrorSummary = errorSummary
        };
    }

    private sealed class ScenarioHarness
    {
        private readonly Channel<BlockDto> _blocks = Channel.CreateUnbounded<BlockDto>();
        private readonly ChatAgentLoopCoordinator _coordinator;

        public ScenarioHarness(
            IRuleResponseEvaluator? ruleEvaluator = null,
            ILlmResponseEvaluator? llmEvaluator = null,
            IRepairContextBuilder? repairContextBuilder = null)
        {
            Metrics = new FakeMetricsCollector();
            var repository = new FakeChatMessageRepository();
            Messages = repository.Messages;
            var notifier = new FakeRuntimeEventNotifier();
            EventTypes = notifier.EventTypes;
            ruleEvaluator ??= new PassedEvaluator();
            llmEvaluator ??= new PassedEvaluator();
            repairContextBuilder ??= new UnusedRepairContextBuilder();

            var guard = new AgentLoopRecoveryGuard(
                new EmptyRuntimeInspector(),
                new AgentLoopRecoveryPipeline(
                [
                    new RuntimeRecoveryMiddleware(),
                    new LoopGuardMiddleware()
                ]));
            var executor = new AgentLoopExecutor(
                ruleEvaluator,
                llmEvaluator,
                repairContextBuilder,
                NullLogger<AgentLoopExecutor>.Instance);
            _coordinator = new ChatAgentLoopCoordinator(
                executor,
                guard,
                Metrics,
                new FakeTracingService(),
                repository,
                new UnusedPendingInteractionService(),
                notifier,
                NullLogger<ChatAgentLoopCoordinator>.Instance);
        }

        public FakeMetricsCollector Metrics { get; }

        public List<ChatMessage> Messages { get; }

        public List<ServerEventType> EventTypes { get; }

        public Task<AgentLoopDecision> HandleAsync(
            IReadOnlyList<ToolExecutionRecord> toolRecords,
            int agentLoopAttempt)
        {
            return _coordinator.HandleAsync(
                sessionId: Guid.NewGuid(),
                userId: Guid.NewGuid(),
                providerId: Guid.NewGuid(),
                userQuery: "运行完整验证并保留下一步动作",
                fullResponse: "工具调用已返回运行态信息",
                new ChatMessage { Id = Guid.NewGuid() },
                toolRecords,
                agentLoopAttempt,
                _blocks.Writer,
                CancellationToken.None);
        }

        public bool TryReadBlock(out BlockDto? block)
        {
            return _blocks.Reader.TryRead(out block);
        }
    }

    private sealed class EmptyRuntimeInspector : IChatSessionRuntimeInspector
    {
        public Task<ChatSessionRuntimeSnapshot> InspectAsync(
            Guid userId,
            Guid sessionId,
            int queuedCount,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ChatSessionRuntimeSnapshot());
        }
    }

    private sealed class PassedEvaluator : IRuleResponseEvaluator, ILlmResponseEvaluator
    {
        public Task<EvaluationResult> EvaluateAsync(
            EvaluationContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new EvaluationResult { Passed = true });
        }
    }

    private sealed class FailingEvaluator : IRuleResponseEvaluator, ILlmResponseEvaluator
    {
        public Task<EvaluationResult> EvaluateAsync(
            EvaluationContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new EvaluationResult
            {
                Passed = false,
                CanRepair = true,
                Feedback = "需要补充上下文",
                Score = 10
            });
        }
    }

    private sealed class UnusedRepairContextBuilder : IRepairContextBuilder
    {
        public string Build(EvaluationContext context, EvaluationResult evaluation)
        {
            throw new InvalidOperationException("确定性恢复路径不应调用通用修复上下文构建器。");
        }
    }

    private sealed class FixedRepairContextBuilder : IRepairContextBuilder
    {
        public string Build(EvaluationContext context, EvaluationResult evaluation)
        {
            return "请改用 repo-parser Skill 处理仓库 URL。";
        }
    }

    private sealed class FakeMetricsCollector : IAgentLoopMetricsCollector
    {
        public List<bool> RepairAttempts { get; } = [];

        public Task RecordRepairAttempt(bool success, long durationMs)
        {
            RepairAttempts.Add(success);
            return Task.CompletedTask;
        }

        public Task RecordMaxAttemptsReached(int totalAttempts) => Task.CompletedTask;

        public Task RecordToolExecution(string toolName, bool success, long durationMs) => Task.CompletedTask;

        public Task RecordTerminalOutput(long outputBytes, int chunkCount, long persistLatencyMs) => Task.CompletedTask;

        public Task RecordSessionRecovery(bool success, int recoveredMessageCount) => Task.CompletedTask;

        public Dictionary<string, object> GetMetricsSnapshot() => [];

        public void ResetMetrics()
        {
            RepairAttempts.Clear();
        }
    }

    private sealed class FakeTracingService : IDistributedTracingService
    {
        public Task LogStructuredEventAsync(
            TraceEvent traceEvent,
            string level = "Information",
            string? message = null,
            Exception? exception = null) => Task.CompletedTask;

        public Task RecordMetricAsync(
            string metricName,
            double value,
            string unit = "",
            Dictionary<string, string>? tags = null) => Task.CompletedTask;

        public IDisposable BeginOperationTimer(string operationName) => new NoopDisposable();

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class FakeChatMessageRepository : IChatMessageRepository
    {
        public List<ChatMessage> Messages { get; } = [];

        public Task AddAsync(ChatMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ChatMessage>> ListBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ChatMessage?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ChatMessage>> ListRecentBySessionAsync(Guid sessionId, int takeCount, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> ListIdsBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ChatMessage?> GetByIdWithSessionAsync(Guid messageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ChatMessage>> ListBySessionAndIdsAsync(Guid sessionId, IReadOnlyCollection<Guid> messageIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ChatMessage?> GetLatestBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ChatMessage?> GetLatestBySessionAndSenderAsync(Guid sessionId, string senderType, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Guid?> GetLatestMessageIdBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateAsync(ChatMessage message, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(ChatMessage message, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteRangeAsync(IReadOnlyCollection<ChatMessage> messages, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class UnusedPendingInteractionService : IPendingInteractionService
    {
        public Task<PendingInteraction> CreateOrReuseAsync(
            Guid sessionId,
            Guid? messageId,
            ToolExecutionRecord toolRecord,
            string? evaluationFeedback,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<PendingInteraction> ResolveAsync(
            Guid? userId,
            Guid sessionId,
            Guid interactionId,
            string action,
            IReadOnlyDictionary<string, string?> values,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeRuntimeEventNotifier : IRuntimeEventNotifier
    {
        public List<ServerEventType> EventTypes { get; } = [];

        public Task NotifyAsync(
            Guid userId,
            Guid sessionId,
            ServerEventType eventType,
            object? data = null,
            CancellationToken cancellationToken = default)
        {
            EventTypes.Add(eventType);
            return Task.CompletedTask;
        }

        public Task NotifyAsync(
            Guid userId,
            ServerEvent serverEvent,
            CancellationToken cancellationToken = default)
        {
            EventTypes.Add(serverEvent.EventType);
            return Task.CompletedTask;
        }
    }
}
