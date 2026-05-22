using DevNexus.Core.Abstractions;
using DevNexus.Core.Services.Swarm.Execution;
using DevNexus.Core.Services.Swarm.Planning;
using DevNexus.Domain.Enums;
using DevNexus.Domain.Models.Swarm;
using DevNexus.Shared.DTOs.Swarm;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Swarm;

/// <summary>
/// Swarm 工作包调度取消测试。
/// </summary>
public sealed class SwarmPackageSchedulerCancellationTests
{
    /// <summary>
    /// 取消已触发时调度器不得继续启动新的工作包。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldAbortPendingPackages_WhenCancellationAlreadyRequested()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var executor = new FakeWorkPackageExecutor();
        var scheduler = CreateScheduler(executor);
        var plan = CreatePlan(
            CreatePackage("package-1"),
            CreatePackage("package-2", SwarmExecutionStrategy.ParallelPackages));

        var act = async () => await scheduler.ExecuteAsync(
            plan,
            Guid.NewGuid(),
            Guid.NewGuid(),
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        executor.ExecutedPackageIds.Should().BeEmpty();
        plan.Packages.Should().OnlyContain(package => package.Status == SwarmPackageStatus.Aborted);
        plan.Packages.Should().OnlyContain(package => package.FailureReason == "Swarm 工作包调度已取消。");
    }

    /// <summary>
    /// 执行中收到取消时当前工作包应标记为已中止而不是失败。
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ShouldMarkCurrentPackageAborted_WhenExecutorObservesCancellation()
    {
        using var cts = new CancellationTokenSource();
        var executor = new FakeWorkPackageExecutor
        {
            OnExecute = async token =>
            {
                await cts.CancelAsync();
                token.ThrowIfCancellationRequested();
                return new SwarmTaskExecutionResult
                {
                    Content = "不会返回",
                    ExecutorName = "fake",
                    Succeeded = true
                };
            }
        };
        var scheduler = CreateScheduler(executor);
        var plan = CreatePlan(CreatePackage("package-1"));

        var act = async () => await scheduler.ExecuteAsync(
            plan,
            Guid.NewGuid(),
            Guid.NewGuid(),
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        executor.ExecutedPackageIds.Should().Equal("package-1");
        plan.Packages[0].Status.Should().Be(SwarmPackageStatus.Aborted);
        plan.Packages[0].FailureReason.Should().Be("Swarm 工作包调度已取消。");
    }

    private static SwarmPackageScheduler CreateScheduler(FakeWorkPackageExecutor executor)
    {
        return new SwarmPackageScheduler(
            new FakeSwarmEventService(),
            executor,
            NullLogger<SwarmPackageScheduler>.Instance);
    }

    private static SwarmExecutionPlan CreatePlan(params ContextWorkPackage[] packages)
    {
        return new SwarmExecutionPlan
        {
            SessionId = Guid.NewGuid().ToString(),
            Packages = packages.ToList()
        };
    }

    private static ContextWorkPackage CreatePackage(
        string id,
        SwarmExecutionStrategy strategy = SwarmExecutionStrategy.SingleAgentSequential)
    {
        return new ContextWorkPackage
        {
            Id = id,
            SessionId = Guid.NewGuid().ToString(),
            Title = id,
            Objective = id,
            ExecutionStrategy = strategy,
            Status = SwarmPackageStatus.Pending
        };
    }

    private sealed class FakeWorkPackageExecutor : IContextWorkPackageExecutor
    {
        public List<string> ExecutedPackageIds { get; } = new();

        public Func<CancellationToken, Task<SwarmTaskExecutionResult>>? OnExecute { get; set; }

        public Task<SwarmTaskExecutionResult> ExecuteAsync(
            ContextWorkPackage package,
            Guid providerId,
            Guid userId,
            CancellationToken cancellationToken = default,
            string? extraInstruction = null)
        {
            ExecutedPackageIds.Add(package.Id);
            return OnExecute?.Invoke(cancellationToken)
                   ?? Task.FromResult(new SwarmTaskExecutionResult
                   {
                       Content = "ok",
                       ExecutorName = "fake",
                       Succeeded = true
                   });
        }
    }

    private sealed class FakeSwarmEventService : ISwarmEventService
    {
        public Task NotifySessionStartedAsync(string sessionId, string description, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifySwarmStartedAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifySwarmCompletedAsync(string sessionId, int resultLength, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifySwarmFailedAsync(string sessionId, string error, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifySwarmCancelledAsync(string sessionId, string reason, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifyContextPackagesUpdatedAsync(
            string sessionId,
            IReadOnlyList<ContextWorkPackageDto> packages,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifyTaskStatusChangedAsync(
            string sessionId,
            string taskId,
            string status,
            string? message = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifyArbitrationEventAsync(
            string sessionId,
            string taskId,
            string eventType,
            string details,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifyAgentStatusChangedAsync(
            string sessionId,
            string agentName,
            string status,
            string currentAction,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifyControlCommandAsync(
            string sessionId,
            SwarmControlCommandDto command,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifySessionFinalizedAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task NotifyConfirmationRequestedAsync(
            string sessionId,
            string confirmationId,
            string operation,
            string payload,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
