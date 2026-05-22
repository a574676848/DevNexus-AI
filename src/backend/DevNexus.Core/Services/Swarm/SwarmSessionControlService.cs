using DevNexus.Core.Abstractions;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Domain.Enums;
using DevNexus.Domain.Models.Swarm;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.DTOs.Swarm;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Logging;
using DevNexus.Core.Services.Swarm.Execution;
using DevNexus.Core.Services.Swarm.Planning;

namespace DevNexus.Core.Services.Swarm;

/// <summary>
/// Swarm 会话控制服务。
/// </summary>
public class SwarmSessionControlService : ISwarmSessionControlService
{
    private readonly SwarmSessionRegistry _sessionRegistry;
    private readonly IContextSwarmSessionRepository _swarmSessionRepository;
    private readonly ISwarmEventService _swarmEventService;
    private readonly IContextWorkPackageExecutor _workPackageExecutor;
    private readonly IRuntimeEventNotifier _runtimeEventNotifier;
    private readonly ILogger<SwarmSessionControlService> _logger;

    /// <summary>
    /// 初始化会话控制服务。
    /// </summary>
    public SwarmSessionControlService(
        SwarmSessionRegistry sessionRegistry,
        IContextSwarmSessionRepository swarmSessionRepository,
        ISwarmEventService swarmEventService,
        IContextWorkPackageExecutor workPackageExecutor,
        IRuntimeEventNotifier runtimeEventNotifier,
        ILogger<SwarmSessionControlService> logger)
    {
        _sessionRegistry = sessionRegistry;
        _swarmSessionRepository = swarmSessionRepository;
        _swarmEventService = swarmEventService;
        _workPackageExecutor = workPackageExecutor;
        _runtimeEventNotifier = runtimeEventNotifier;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task PauseAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _swarmSessionRepository.GetBySessionIdAsync(sessionId);
        var decision = SwarmControlCommandPolicy.CanPause(session?.Status);
        if (!decision.Accepted)
        {
            await NotifyControlCommandAsync(
                sessionId,
                decision.Command,
                isPaused: false,
                cancellationToken,
                accepted: false,
                message: decision.Message);
            return;
        }

        if (session == null)
        {
            return;
        }

        _sessionRegistry.Pause(sessionId);
        await _swarmSessionRepository.UpdateSessionStatusAsync(sessionId, SwarmStatus.Paused, session.Result);
        await NotifyControlCommandAsync(sessionId, "Paused", isPaused: true, cancellationToken);
        await NotifyRuntimeEventAsync(sessionId, ServerEventType.SessionSuspended, "Paused", cancellationToken);

        _logger.LogInformation("Swarm 会话已暂停 | SessionId={SessionId}", sessionId);
    }

    /// <inheritdoc />
    public async Task ResumeAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _swarmSessionRepository.GetBySessionIdAsync(sessionId);
        var decision = SwarmControlCommandPolicy.CanResume(session?.Status);
        if (!decision.Accepted)
        {
            await NotifyControlCommandAsync(
                sessionId,
                decision.Command,
                isPaused: false,
                cancellationToken,
                accepted: false,
                message: decision.Message);
            return;
        }

        if (session == null)
        {
            return;
        }

        _sessionRegistry.Resume(sessionId);
        await _swarmSessionRepository.UpdateSessionStatusAsync(sessionId, SwarmStatus.Running, session.Result);
        await NotifyControlCommandAsync(sessionId, "Resumed", isPaused: false, cancellationToken);
        await NotifyRuntimeEventAsync(sessionId, ServerEventType.SessionResumed, "Resumed", cancellationToken);

        _logger.LogInformation("Swarm 会话已恢复 | SessionId={SessionId}", sessionId);
    }

    /// <inheritdoc />
    public async Task AbortAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        _sessionRegistry.Abort(sessionId);
        var session = await _swarmSessionRepository.GetBySessionIdAsync(sessionId);
        if (session != null)
        {
            var finalization = SwarmSessionFinalizationPolicy.BuildUserAbort(session.Packages);
            session.Status = finalization.Status;
            session.Result = finalization.Reason;
            session.CompletedAt = DateTime.UtcNow;
            session.UpdatedAt = DateTime.UtcNow;
            await _swarmSessionRepository.SaveAsync(session);
        }
        else
        {
            await _swarmSessionRepository.UpdateSessionStatusAsync(sessionId, SwarmStatus.Aborted);
        }

        await NotifyControlCommandAsync(sessionId, "Aborted", isPaused: false, cancellationToken);
        await NotifyRuntimeEventAsync(sessionId, ServerEventType.SessionCancelled, "Aborted", cancellationToken);

        _logger.LogInformation("Swarm 会话已中止 | SessionId={SessionId}", sessionId);
    }

    private async Task<SwarmControlCommandDto> NotifyControlCommandAsync(
        string sessionId,
        string command,
        bool isPaused,
        CancellationToken cancellationToken,
        bool accepted = true,
        string? message = null)
    {
        var session = await _swarmSessionRepository.GetBySessionIdAsync(sessionId);
        var payload = SwarmControlCommandBuilder.Build(
            sessionId,
            command,
            session?.Packages ?? Array.Empty<ContextWorkPackageRecord>(),
            isPaused,
            accepted,
            message);

        await _swarmEventService.NotifyControlCommandAsync(sessionId, payload, cancellationToken);
        return payload;
    }

    private async Task NotifyRuntimeEventAsync(
        string sessionId,
        ServerEventType eventType,
        string command,
        CancellationToken cancellationToken)
    {
        var session = await _swarmSessionRepository.GetBySessionIdAsync(sessionId);
        if (session == null)
        {
            return;
        }

        await _runtimeEventNotifier.NotifyAsync(
            session.UserId,
            Guid.TryParse(sessionId, out var parsedSessionId) ? parsedSessionId : Guid.Empty,
            eventType,
            new
            {
                SessionId = sessionId,
                Command = command
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SwarmControlCommandDto> RetryPackageAsync(
        string sessionId,
        string packageId,
        CancellationToken cancellationToken = default)
    {
        var session = await _swarmSessionRepository.GetBySessionIdAsync(sessionId);
        var packageRecord = session?.Packages.FirstOrDefault(task => string.Equals(task.TaskId, packageId, StringComparison.Ordinal));
        var decision = SwarmControlCommandPolicy.CanRetryPackage(session?.Status, packageRecord?.Status);
        if (!decision.Accepted)
        {
            return await NotifyControlCommandAsync(
                sessionId,
                decision.Command,
                isPaused: false,
                cancellationToken,
                accepted: false,
                message: decision.Message);
        }

        if (session == null || packageRecord == null)
        {
            return await NotifyControlCommandAsync(
                sessionId,
                "RetryRejected",
                isPaused: false,
                cancellationToken,
                accepted: false,
                message: "Swarm 重试上下文缺失，请刷新后重试。");
        }

        if (!_sessionRegistry.TryBeginPackageRetry(sessionId, packageId))
        {
            return await NotifyControlCommandAsync(
                sessionId,
                "RetryRejected",
                isPaused: false,
                cancellationToken,
                accepted: false,
                message: "该工作包已有重试任务在执行，请稍后刷新状态。");
        }

        await NotifyControlCommandAsync(
            sessionId,
            decision.Command,
            isPaused: false,
            cancellationToken);

        var package = MapToDomainPackage(session, packageRecord);

        try
        {
            package.Status = SwarmPackageStatus.InProgress;
            package.FailureReason = null;
            package.Result = null;
            package.UpdatedAt = DateTime.UtcNow;

            ApplyPackageToRecord(packageRecord, package);
            session.Status = SwarmStatus.Running;
            session.CompletedAt = null;
            await _swarmSessionRepository.SaveAsync(session);
            await NotifyPackagesAsync(sessionId, session.Packages.ToList(), cancellationToken);

            var executionResult = await _workPackageExecutor.ExecuteAsync(
                package,
                session.ProviderId,
                session.UserId,
                cancellationToken);

            package.Result = executionResult.Content;
            package.ExecutorName = executionResult.ExecutorName;
            package.FailureReason = executionResult.FailureReason;
            package.ExecutionReportArtifactId = executionResult.ArtifactId;
            package.CommandLine = executionResult.Metadata.TryGetValue("commandLine", out var commandLine) ? commandLine : package.CommandLine;
            package.WorkingDirectory = executionResult.Metadata.TryGetValue("workingDirectory", out var workingDirectory) ? workingDirectory : package.WorkingDirectory;
            package.Status = executionResult.Succeeded ? SwarmPackageStatus.Completed : SwarmPackageStatus.Failed;
            package.UpdatedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            package.Status = SwarmPackageStatus.Failed;
            package.FailureReason = ex.Message;
            package.Result ??= ex.Message;
            package.UpdatedAt = DateTime.UtcNow;

            _logger.LogError(ex, "工作包重试失败 | SessionId={SessionId} PackageId={PackageId}", sessionId, packageId);
        }
        finally
        {
            _sessionRegistry.EndPackageRetry(sessionId, packageId);
        }

        ApplyPackageToRecord(packageRecord, package);
        session.Status = ResolveSessionStatus(session.Packages);
        if (session.Status is SwarmStatus.Completed or SwarmStatus.Failed)
        {
            session.CompletedAt = DateTime.UtcNow;
        }
        await _swarmSessionRepository.SaveAsync(session);
        await NotifyPackagesAsync(sessionId, session.Packages.ToList(), cancellationToken);
        return await NotifyControlCommandAsync(
            sessionId,
            "RetryCompleted",
            isPaused: false,
            cancellationToken);
    }

    /// <inheritdoc />
    public SwarmControlStatus? GetStatus(string sessionId)
    {
        return _sessionRegistry.GetStatus(sessionId);
    }

    private async Task NotifyPackagesAsync(
        string sessionId,
        IReadOnlyCollection<ContextWorkPackageRecord> packageRecords,
        CancellationToken cancellationToken)
    {
        var packages = packageRecords
            .OrderBy(package => package.CreatedAt)
            .Select(package => new ContextWorkPackageDto
            {
                Id = package.TaskId,
                Title = package.Title,
                Objective = package.Description,
                ContextType = package.ContextType,
                Status = package.Status.ToString(),
                ExecutionStrategy = package.ExecutionStrategy,
                Dependencies = package.Dependencies,
                Result = package.Result,
                FailureReason = package.FailureReason,
                RiskLevel = 0,
                ExecutorName = package.ExecutorName,
                CommandLine = package.CommandLine,
                WorkingDirectory = package.WorkingDirectory,
                ExecutionReportArtifactId = package.ExecutionReportArtifactId,
                UpdatedAt = package.UpdatedAt,
                CanRetry = package.Status == SwarmTaskStatus.Failed,
                OwnedFiles = package.OwnedFiles,
                OwnedSymbols = package.OwnedSymbols
            })
            .ToList();

        await _swarmEventService.NotifyContextPackagesUpdatedAsync(sessionId, packages, cancellationToken);
    }

    private static ContextWorkPackage MapToDomainPackage(ContextSwarmSession session, ContextWorkPackageRecord record)
    {
        return new ContextWorkPackage
        {
            Id = record.TaskId,
            SessionId = session.SessionId,
            Title = record.Title,
            Objective = record.Description,
            ContextType = Enum.TryParse<SwarmContextType>(record.ContextType, out var contextType)
                ? contextType
                : SwarmContextType.Unknown,
            ExecutionStrategy = Enum.TryParse<SwarmExecutionStrategy>(record.ExecutionStrategy, out var strategy)
                ? strategy
                : SwarmExecutionStrategy.SingleAgentSequential,
            Status = MapToDomainStatus(record.Status),
            Result = record.Result,
            FailureReason = record.FailureReason,
            ExecutorName = record.ExecutorName,
            CommandLine = record.CommandLine,
            WorkingDirectory = record.WorkingDirectory,
            ExecutionReportArtifactId = record.ExecutionReportArtifactId,
            UpdatedAt = record.UpdatedAt,
            CreatedAt = record.CreatedAt,
            OwnedFiles = record.OwnedFiles.ToList(),
            OwnedSymbols = record.OwnedSymbols.ToList(),
            InputContracts = record.InputContracts.Select(name => new ContextContract { Name = name }).ToList(),
            OutputContracts = record.OutputContracts.Select(name => new ContextContract { Name = name }).ToList(),
            Dependencies = record.Dependencies.Select(sourceId => new ContextDependency
            {
                SourcePackageId = sourceId,
                TargetPackageId = record.TaskId,
                Reason = "retry"
            }).ToList()
        };
    }

    private static void ApplyPackageToRecord(ContextWorkPackageRecord record, ContextWorkPackage package)
    {
        record.Status = MapToRecordStatus(package.Status);
        record.Result = package.Result;
        record.FailureReason = package.FailureReason;
        record.ExecutorName = package.ExecutorName;
        record.CommandLine = package.CommandLine;
        record.WorkingDirectory = package.WorkingDirectory;
        record.ExecutionReportArtifactId = package.ExecutionReportArtifactId;
        record.OwnedFiles = package.OwnedFiles.ToList();
        record.OwnedSymbols = package.OwnedSymbols.ToList();
        SwarmPackageRecordLifecyclePolicy.Apply(record, DateTime.UtcNow);
    }

    private static SwarmStatus ResolveSessionStatus(IEnumerable<ContextWorkPackageRecord> packages)
    {
        var packageList = packages.ToList();
        if (packageList.Any(package => package.Status == SwarmTaskStatus.Failed))
        {
            return SwarmStatus.Failed;
        }

        if (packageList.All(package => package.Status == SwarmTaskStatus.Completed))
        {
            return SwarmStatus.Completed;
        }

        return SwarmStatus.Running;
    }

    private static SwarmPackageStatus MapToDomainStatus(SwarmTaskStatus status)
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

    private static SwarmTaskStatus MapToRecordStatus(SwarmPackageStatus status)
    {
        return status switch
        {
            SwarmPackageStatus.Pending => SwarmTaskStatus.Pending,
            SwarmPackageStatus.Ready => SwarmTaskStatus.Ready,
            SwarmPackageStatus.InProgress => SwarmTaskStatus.InProgress,
            SwarmPackageStatus.Completed => SwarmTaskStatus.Completed,
            SwarmPackageStatus.Evaluating => SwarmTaskStatus.Evaluating,
            SwarmPackageStatus.Aborted => SwarmTaskStatus.Skipped,
            _ => SwarmTaskStatus.Failed
        };
    }
}
