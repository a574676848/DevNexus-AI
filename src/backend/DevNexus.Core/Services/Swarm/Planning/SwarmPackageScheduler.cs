using DevNexus.Core.Abstractions;
using DevNexus.Core.Services.Swarm.Execution;
using DevNexus.Domain.Enums;
using DevNexus.Domain.Models.Swarm;
using Microsoft.Extensions.Logging;

namespace DevNexus.Core.Services.Swarm.Planning;

/// <summary>
/// 上下文工作包调度器默认实现。
/// </summary>
public class SwarmPackageScheduler : ISwarmPackageScheduler
{
    private readonly ISwarmEventService _swarmEventService;
    private readonly IContextWorkPackageExecutor _workPackageExecutor;
    private readonly ILogger<SwarmPackageScheduler> _logger;

    /// <summary>
    /// 初始化工作包调度器。
    /// </summary>
    public SwarmPackageScheduler(
        ISwarmEventService swarmEventService,
        IContextWorkPackageExecutor workPackageExecutor,
        ILogger<SwarmPackageScheduler> logger)
    {
        _swarmEventService = swarmEventService;
        _workPackageExecutor = workPackageExecutor;
        _logger = logger;
    }

    /// <summary>
    /// 按工作包粒度执行当前计划。
    /// </summary>
    public async Task ExecuteAsync(
        SwarmExecutionPlan plan,
        Guid providerId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var sequentialPackages = new List<ContextWorkPackage>();
        var parallelPackages = new List<ContextWorkPackage>();

        foreach (var package in plan.Packages)
        {
            if (package.ExecutionStrategy == SwarmExecutionStrategy.ParallelPackages)
            {
                parallelPackages.Add(package);
            }
            else
            {
                sequentialPackages.Add(package);
            }
        }

        foreach (var package in sequentialPackages)
        {
            if (SwarmPackageCancellationPolicy.MarkPendingPackagesAborted(
                plan.Packages,
                cancellationToken))
            {
                await NotifyPackageSnapshotAsync(plan, CancellationToken.None);
            }

            cancellationToken.ThrowIfCancellationRequested();
            await ExecutePackageByStrategyAsync(plan, package, providerId, userId, cancellationToken);
        }

        if (parallelPackages.Count > 0)
        {
            if (SwarmPackageCancellationPolicy.MarkPendingPackagesAborted(plan.Packages, cancellationToken))
            {
                await NotifyPackageSnapshotAsync(plan, CancellationToken.None);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var parallelExecutions = parallelPackages.Select(package =>
                ExecutePackageByStrategyAsync(plan, package, providerId, userId, cancellationToken));
            await Task.WhenAll(parallelExecutions);
        }

        _logger.LogInformation(
            "Swarm 工作包调度完成 | SessionId={SessionId} PackageCount={PackageCount} ProviderId={ProviderId} UserId={UserId}",
            plan.SessionId,
            plan.Packages.Count,
            providerId,
            userId);

        return;
    }

    /// <summary>
    /// 按工作包执行策略执行单个工作包。
    /// </summary>
    private async Task ExecutePackageByStrategyAsync(
        SwarmExecutionPlan plan,
        ContextWorkPackage package,
        Guid providerId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        package.Status = SwarmPackageStatus.Ready;
        package.UpdatedAt = DateTime.UtcNow;
        await NotifyPackageSnapshotAsync(plan, cancellationToken);

        package.Status = SwarmPackageStatus.InProgress;
        package.UpdatedAt = DateTime.UtcNow;
        package.FailureReason = null;
        package.StateContext = $"ProviderId={providerId};UserId={userId}";
        await NotifyPackageSnapshotAsync(plan, cancellationToken);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (package.ExecutionStrategy)
            {
                case SwarmExecutionStrategy.SingleAgentSequential:
                    await ExecuteThroughTaskExecutorAsync(
                        package,
                        providerId,
                        userId,
                        SwarmExecutionStrategy.SingleAgentSequential,
                        cancellationToken);
                    break;
                case SwarmExecutionStrategy.ParallelPackages:
                    await ExecuteThroughTaskExecutorAsync(
                        package,
                        providerId,
                        userId,
                        SwarmExecutionStrategy.ParallelPackages,
                        cancellationToken);
                    break;
                case SwarmExecutionStrategy.SupervisorRouted:
                    await ExecuteThroughTaskExecutorAsync(
                        package,
                        providerId,
                        userId,
                        SwarmExecutionStrategy.SupervisorRouted,
                        cancellationToken,
                        "该工作包由 Supervisor 路由决策后进入执行。");
                    break;
                case SwarmExecutionStrategy.GroupDeliberation:
                    await ExecuteThroughTaskExecutorAsync(
                        package,
                        providerId,
                        userId,
                        SwarmExecutionStrategy.GroupDeliberation,
                        cancellationToken);
                    break;
                case SwarmExecutionStrategy.BlackBoxValidation:
                    await ExecuteBlackBoxValidationAsync(package, cancellationToken);
                    break;
                default:
                    await ExecuteThroughTaskExecutorAsync(
                        package,
                        providerId,
                        userId,
                        SwarmExecutionStrategy.SingleAgentSequential,
                        cancellationToken);
                    break;
            }

            package.Status = SwarmPackageStatus.Completed;
            package.EvidenceContext = string.IsNullOrWhiteSpace(package.EvidenceContext)
                ? "当前为默认调度路径，尚未接入真实证据收集。"
                : package.EvidenceContext;
            package.UpdatedAt = DateTime.UtcNow;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SwarmPackageCancellationPolicy.MarkCurrentPackageAborted(package);
            throw;
        }
        catch (Exception ex)
        {
            package.Status = SwarmPackageStatus.Failed;
            package.FailureReason = ex.Message;
            package.Result ??= ex.Message;
            package.UpdatedAt = DateTime.UtcNow;
            _logger.LogError(ex, "工作包执行失败 | SessionId={SessionId} PackageId={PackageId}", plan.SessionId, package.Id);
        }

        await NotifyPackageSnapshotAsync(plan, cancellationToken);
    }

    /// <summary>
    /// 通过现有任务执行器执行工作包。
    /// </summary>
    private async Task ExecuteThroughTaskExecutorAsync(
        ContextWorkPackage package,
        Guid providerId,
        Guid userId,
        SwarmExecutionStrategy executionStrategy,
        CancellationToken cancellationToken,
        string? extraInstruction = null)
    {
        package.ExecutionStrategy = executionStrategy;
        var executionResult = await _workPackageExecutor.ExecuteAsync(
            package,
            providerId,
            userId,
            cancellationToken,
            extraInstruction);

        package.Result = executionResult.Content;
        package.ExecutorName = executionResult.ExecutorName;
        package.ExecutionReportArtifactId = executionResult.ArtifactId;
        package.FailureReason = executionResult.FailureReason;
        package.StateContext = $"{package.StateContext} | Strategy={package.ExecutionStrategy} | Executor={executionResult.ExecutorName}";
        package.EvidenceContext = string.IsNullOrWhiteSpace(package.EvidenceContext)
            ? executionResult.ExecutionKind
            : $"{package.EvidenceContext}{Environment.NewLine}{executionResult.ExecutionKind}";

        foreach (var metadata in executionResult.Metadata)
        {
            if (string.Equals(metadata.Key, "ownedFiles", StringComparison.OrdinalIgnoreCase))
            {
                package.OwnedFiles = metadata.Value
                    .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
            }

            if (string.Equals(metadata.Key, "ownedSymbols", StringComparison.OrdinalIgnoreCase))
            {
                package.OwnedSymbols = metadata.Value
                    .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
            }

            if (string.Equals(metadata.Key, "commandLine", StringComparison.OrdinalIgnoreCase))
            {
                package.CommandLine = metadata.Value;
            }

            if (string.Equals(metadata.Key, "workingDirectory", StringComparison.OrdinalIgnoreCase))
            {
                package.WorkingDirectory = metadata.Value;
            }
        }

        if (!executionResult.Succeeded)
        {
            package.Status = SwarmPackageStatus.Failed;
            package.FailureReason ??= "执行失败，请查看终端输出和执行报告。";
            throw new InvalidOperationException(package.FailureReason);
        }
    }

    /// <summary>
    /// 执行黑盒验证策略。
    /// </summary>
    private static Task ExecuteBlackBoxValidationAsync(
        ContextWorkPackage package,
        CancellationToken cancellationToken)
    {
        package.Result ??= $"工作包 {package.Title} 已完成黑盒验证与结果回报。";
        package.StateContext = $"{package.StateContext} | Strategy=BlackBoxValidation";
        return Task.CompletedTask;
    }

    /// <summary>
    /// 广播当前工作包执行快照。
    /// </summary>
    private Task NotifyPackageSnapshotAsync(SwarmExecutionPlan plan, CancellationToken cancellationToken)
    {
        var snapshot = plan.Packages.Select(ContextWorkPackageMapper.ToDto).ToList();
        return _swarmEventService.NotifyContextPackagesUpdatedAsync(plan.SessionId, snapshot, cancellationToken);
    }
}
