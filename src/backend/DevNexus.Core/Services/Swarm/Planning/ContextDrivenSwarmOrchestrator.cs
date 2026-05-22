using DevNexus.Core.Services.Swarm.Analysis;
using DevNexus.Core.Abstractions;
using DevNexus.Core.Services.Swarm.Context;
using DevNexus.Core.Services.Swarm.Evaluation;
using DevNexus.Core.Services.Swarm.Execution;
using DevNexus.Core.Services.Swarm.Routing;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Domain.Enums;
using DevNexus.Domain.Models.Swarm;
using DevNexus.Shared.Enums;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace DevNexus.Core.Services.Swarm.Planning;

/// <summary>
/// 基于上下文工作包的 Swarm 编排服务。
/// </summary>
public class ContextDrivenSwarmOrchestrator : ISwarmOrchestrator
{
    private readonly IContextAnalyzer _contextAnalyzer;
    private readonly IContextSegmenter _contextSegmenter;
    private readonly IWorkPackagePlanner _workPackagePlanner;
    private readonly IExecutionStrategySelector _executionStrategySelector;
    private readonly IContextRoutingService _contextRoutingService;
    private readonly ISwarmPackageScheduler _packageScheduler;
    private readonly IContextEvaluationService _contextEvaluationService;
    private readonly IContextSwarmSessionRepository _swarmSessionRepository;
    private readonly ISwarmEventService _swarmEventService;
    private readonly ILogger<ContextDrivenSwarmOrchestrator> _logger;

    /// <summary>
    /// 初始化基于上下文驱动的 Swarm 编排服务。
    /// </summary>
    public ContextDrivenSwarmOrchestrator(
        IContextAnalyzer contextAnalyzer,
        IContextSegmenter contextSegmenter,
        IWorkPackagePlanner workPackagePlanner,
        IExecutionStrategySelector executionStrategySelector,
        IContextRoutingService contextRoutingService,
        ISwarmPackageScheduler packageScheduler,
        IContextEvaluationService contextEvaluationService,
        IContextSwarmSessionRepository swarmSessionRepository,
        ISwarmEventService swarmEventService,
        ILogger<ContextDrivenSwarmOrchestrator> logger)
    {
        _contextAnalyzer = contextAnalyzer;
        _contextSegmenter = contextSegmenter;
        _workPackagePlanner = workPackagePlanner;
        _executionStrategySelector = executionStrategySelector;
        _contextRoutingService = contextRoutingService;
        _packageScheduler = packageScheduler;
        _contextEvaluationService = contextEvaluationService;
        _swarmSessionRepository = swarmSessionRepository;
        _swarmEventService = swarmEventService;
        _logger = logger;
    }

    /// <summary>
    /// 按上下文工作包执行 Swarm 编排。
    /// </summary>
    public async Task<string> OrchestrateAsync(
        string userRequest,
        Guid providerId,
        string sessionId,
        Guid userId,
        ComplexityVector complexity,
        ChannelWriter<BlockDto> blockWriter,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Swarm 上下文编排开始 | SessionId={SessionId} Score={Score}",
            sessionId,
            complexity.CompositeScore);

        var persistedSession = BuildSessionEntity(
            sessionId,
            userRequest,
            userId,
            providerId,
            complexity,
            Array.Empty<ContextWorkPackage>());
        await _swarmSessionRepository.SaveAsync(persistedSession);

        try
        {
            await blockWriter.WriteAsync(
                new BlockDto
                {
                    BlockType = BlockType.Thinking,
                    Content = "Swarm 正在进行上下文分析与工作包规划。",
                    SessionId = Guid.TryParse(sessionId, out var sessionGuid) ? sessionGuid : Guid.Empty
                },
                cancellationToken);

            var draftPackages = await _contextAnalyzer.AnalyzeAsync(
                userRequest,
                sessionId,
                complexity,
                cancellationToken);
            await PersistPackagesAsync(persistedSession, draftPackages, SwarmStatus.Running);
            await NotifyContextPackagesAsync(sessionId, draftPackages, cancellationToken);

            var segmentedPackages = await _contextSegmenter.SegmentAsync(draftPackages, cancellationToken);
            var selectedPackages = new List<ContextWorkPackage>(segmentedPackages.Count);

            foreach (var package in segmentedPackages)
            {
                selectedPackages.Add(await _executionStrategySelector.SelectAsync(package, cancellationToken));
            }
            await PersistPackagesAsync(persistedSession, selectedPackages, SwarmStatus.Running);
            await NotifyContextPackagesAsync(sessionId, selectedPackages, cancellationToken);

            var plan = await _workPackagePlanner.PlanAsync(sessionId, selectedPackages, cancellationToken);
            await PersistPackagesAsync(persistedSession, plan.Packages, SwarmStatus.Running);
            await NotifyContextPackagesAsync(sessionId, plan.Packages, cancellationToken);
            await _contextRoutingService.RouteAsync(plan, cancellationToken);
            await _packageScheduler.ExecuteAsync(plan, providerId, userId, cancellationToken);
            await PersistPackagesAsync(persistedSession, plan.Packages, SwarmStatus.Running);
            await NotifyContextPackagesAsync(sessionId, plan.Packages, cancellationToken);
            await _contextEvaluationService.EvaluateAsync(plan, cancellationToken);
            await PersistPackagesAsync(persistedSession, plan.Packages, SwarmStatus.Completed);
            await NotifyContextPackagesAsync(sessionId, plan.Packages, cancellationToken);

            var summary = string.Join(Environment.NewLine, plan.Packages.Select(
                package => $"- [{package.ExecutionStrategy}] {package.Title}: {package.Result}"));

            await blockWriter.WriteAsync(
                new BlockDto
                {
                    BlockType = BlockType.TextDelta,
                    Content = $"# Swarm 上下文规划结果{Environment.NewLine}{summary}",
                    SessionId = Guid.TryParse(sessionId, out sessionGuid) ? sessionGuid : Guid.Empty
                },
                cancellationToken);

            await _swarmSessionRepository.UpdateSessionStatusAsync(sessionId, SwarmStatus.Completed, summary);

            return summary;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await FinalizeInterruptedSessionAsync(
                persistedSession,
                SwarmSessionFinalizationPolicy.BuildCancelled(ToDomainPackages(persistedSession)),
                CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Swarm 上下文编排失败 | SessionId={SessionId}", sessionId);
            var finalization = SwarmSessionFinalizationPolicy.BuildFailed(ToDomainPackages(persistedSession), ex);
            await FinalizeInterruptedSessionAsync(persistedSession, finalization, cancellationToken);
            return finalization.Reason;
        }
    }

    /// <summary>
    /// 广播当前工作包快照。
    /// </summary>
    private Task NotifyContextPackagesAsync(
        string sessionId,
        IReadOnlyList<ContextWorkPackage> packages,
        CancellationToken cancellationToken)
    {
        var snapshot = packages.Select(ContextWorkPackageMapper.ToDto).ToList();
        return _swarmEventService.NotifyContextPackagesUpdatedAsync(sessionId, snapshot, cancellationToken);
    }

    /// <summary>
    /// 将上下文工作包持久化到会话实体。
    /// </summary>
    private async Task PersistPackagesAsync(
        ContextSwarmSession session,
        IReadOnlyList<ContextWorkPackage> packages,
        SwarmStatus status)
    {
        session.Status = status;
        if (status == SwarmStatus.Completed)
        {
            session.CompletedAt = DateTime.UtcNow;
        }

        session.Packages = packages.Select(MapToSwarmTask).ToList();
        await _swarmSessionRepository.SaveAsync(session);
    }

    /// <summary>
    /// 按收尾策略持久化中断会话。
    /// </summary>
    private async Task FinalizeInterruptedSessionAsync(
        ContextSwarmSession session,
        SwarmSessionFinalizationResult finalization,
        CancellationToken cancellationToken)
    {
        session.Status = finalization.Status;
        session.Result = finalization.Reason;
        session.CompletedAt = DateTime.UtcNow;
        session.Packages = ToDomainPackages(session).Select(MapToSwarmTask).ToList();

        await _swarmSessionRepository.SaveAsync(session);
        await NotifyContextPackagesAsync(session.SessionId, ToDomainPackages(session), cancellationToken);

        if (finalization.NotifyCancellation)
        {
            await _swarmEventService.NotifySwarmCancelledAsync(session.SessionId, finalization.Reason, cancellationToken);
            return;
        }

        if (finalization.NotifyFailure)
        {
            await _swarmEventService.NotifySwarmFailedAsync(session.SessionId, finalization.Reason, cancellationToken);
        }
    }

    /// <summary>
    /// 构建初始会话实体。
    /// </summary>
    private static ContextSwarmSession BuildSessionEntity(
        string sessionId,
        string userRequest,
        Guid userId,
        Guid providerId,
        ComplexityVector complexity,
        IReadOnlyList<ContextWorkPackage> packages)
    {
        return new ContextSwarmSession
        {
            SessionId = sessionId,
            Title = userRequest.Length <= 80 ? userRequest : userRequest[..80],
            Description = userRequest,
            Status = SwarmStatus.Running,
            StartedAt = DateTime.UtcNow,
            UserId = userId,
            DomainType = (int)complexity.PrimaryDomain,
            ProviderId = providerId,
            Packages = packages.Select(MapToSwarmTask).ToList()
        };
    }

    /// <summary>
    /// 将持久化工作包恢复为领域工作包。
    /// </summary>
    private static List<ContextWorkPackage> ToDomainPackages(ContextSwarmSession session)
    {
        return session.Packages.Select(package => new ContextWorkPackage
        {
            Id = package.TaskId,
            SessionId = session.SessionId,
            Title = package.Title,
            Objective = package.Description,
            ContextType = Enum.TryParse<SwarmContextType>(package.ContextType, out var contextType)
                ? contextType
                : SwarmContextType.Unknown,
            ExecutionStrategy = Enum.TryParse<SwarmExecutionStrategy>(package.ExecutionStrategy, out var strategy)
                ? strategy
                : SwarmExecutionStrategy.SingleAgentSequential,
            Status = MapToPackageStatus(package.Status),
            Result = package.Result,
            FailureReason = package.FailureReason,
            ExecutorName = package.ExecutorName,
            CommandLine = package.CommandLine,
            WorkingDirectory = package.WorkingDirectory,
            ExecutionReportArtifactId = package.ExecutionReportArtifactId,
            OwnedFiles = package.OwnedFiles.ToList(),
            OwnedSymbols = package.OwnedSymbols.ToList(),
            InputContracts = package.InputContracts.Select(name => new ContextContract { Name = name }).ToList(),
            OutputContracts = package.OutputContracts.Select(name => new ContextContract { Name = name }).ToList(),
            Dependencies = package.Dependencies.Select(sourceId => new ContextDependency
            {
                SourcePackageId = sourceId,
                TargetPackageId = package.TaskId,
                Reason = "restore"
            }).ToList(),
            CreatedAt = package.CreatedAt,
            UpdatedAt = package.UpdatedAt
        }).ToList();
    }

    /// <summary>
    /// 将工作包映射为持久化任务实体。
    /// </summary>
    private static ContextWorkPackageRecord MapToSwarmTask(ContextWorkPackage package)
    {
        return new ContextWorkPackageRecord
        {
            TaskId = package.Id,
            Title = package.Title,
            Description = package.Objective,
            Role = package.ExecutionStrategy == DevNexus.Domain.Enums.SwarmExecutionStrategy.GroupDeliberation
                ? "Facilitator"
                : "ContextWorker",
            ContextType = package.ContextType.ToString(),
            ExecutionStrategy = package.ExecutionStrategy.ToString(),
            Status = MapToTaskStatus(package.Status),
            Dependencies = package.Dependencies.Select(dependency => dependency.SourcePackageId).Distinct().ToList(),
            Result = package.Result,
            LogicalUnits = new List<string> { package.ContextType.ToString() },
            InputContracts = package.InputContracts.Select(contract => contract.Name).ToList(),
            OutputContracts = package.OutputContracts.Select(contract => contract.Name).ToList(),
            OwnedFiles = package.OwnedFiles.ToList(),
            OwnedSymbols = package.OwnedSymbols.ToList(),
            StartedAt = package.Status is DevNexus.Domain.Enums.SwarmPackageStatus.InProgress or DevNexus.Domain.Enums.SwarmPackageStatus.Completed
                ? package.UpdatedAt
                : null,
            CompletedAt = package.Status == DevNexus.Domain.Enums.SwarmPackageStatus.Completed
                ? package.UpdatedAt
                : null
        };
    }

    /// <summary>
    /// 将工作包状态映射为旧任务状态枚举。
    /// </summary>
    private static SwarmTaskStatus MapToTaskStatus(DevNexus.Domain.Enums.SwarmPackageStatus status)
    {
        return status switch
        {
            DevNexus.Domain.Enums.SwarmPackageStatus.Pending => SwarmTaskStatus.Pending,
            DevNexus.Domain.Enums.SwarmPackageStatus.Ready => SwarmTaskStatus.Ready,
            DevNexus.Domain.Enums.SwarmPackageStatus.InProgress => SwarmTaskStatus.InProgress,
            DevNexus.Domain.Enums.SwarmPackageStatus.Completed => SwarmTaskStatus.Completed,
            DevNexus.Domain.Enums.SwarmPackageStatus.Evaluating => SwarmTaskStatus.Evaluating,
            DevNexus.Domain.Enums.SwarmPackageStatus.Aborted => SwarmTaskStatus.Skipped,
            _ => SwarmTaskStatus.Failed
        };
    }

    /// <summary>
    /// 将旧任务状态映射为工作包状态。
    /// </summary>
    private static SwarmPackageStatus MapToPackageStatus(SwarmTaskStatus status)
    {
        return status switch
        {
            SwarmTaskStatus.Pending => SwarmPackageStatus.Pending,
            SwarmTaskStatus.Ready => SwarmPackageStatus.Ready,
            SwarmTaskStatus.InProgress => SwarmPackageStatus.InProgress,
            SwarmTaskStatus.Completed => SwarmPackageStatus.Completed,
            SwarmTaskStatus.Evaluating => SwarmPackageStatus.Evaluating,
            SwarmTaskStatus.Skipped => SwarmPackageStatus.Aborted,
            _ => SwarmPackageStatus.Failed
        };
    }
}
