using DevNexus.Domain.Abstractions;
using DevNexus.Shared.DTOs.Swarm;
using Microsoft.Extensions.Logging;

namespace DevNexus.Core.Services.Swarm;

/// <summary>
/// Swarm 会话视图服务。
/// </summary>
public class SwarmSessionViewService : ISwarmSessionViewService
{
    private readonly IContextSwarmSessionRepository _swarmSessionRepository;
    private readonly ISwarmSessionControlService _sessionControlService;
    private readonly ILogger<SwarmSessionViewService> _logger;

    /// <summary>
    /// 初始化会话视图服务。
    /// </summary>
    public SwarmSessionViewService(
        IContextSwarmSessionRepository swarmSessionRepository,
        ISwarmSessionControlService sessionControlService,
        ILogger<SwarmSessionViewService> logger)
    {
        _swarmSessionRepository = swarmSessionRepository;
        _sessionControlService = sessionControlService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContextWorkPackageDto>> GetContextPackagesAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var snapshot = await GetContextPackageSnapshotAsync(sessionId, cancellationToken);
        return snapshot.Packages;
    }

    /// <inheritdoc />
    public async Task<ContextSwarmPackageSnapshotDto> GetContextPackageSnapshotAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _swarmSessionRepository.GetBySessionIdAsync(sessionId);
        if (session == null)
        {
            _logger.LogDebug("Swarm 会话不存在，无法获取工作包快照 | SessionId={SessionId}", sessionId);
            return BuildSnapshot(sessionId, Array.Empty<ContextWorkPackageDto>());
        }

        var packages = session.Packages
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

        return BuildSnapshot(sessionId, packages);
    }

    private ContextSwarmPackageSnapshotDto BuildSnapshot(
        string sessionId,
        IReadOnlyCollection<ContextWorkPackageDto> packages)
    {
        var sessionSnapshot = _sessionControlService.GetSnapshot(sessionId);
        return new ContextSwarmPackageSnapshotDto
        {
            SessionId = sessionId,
            Packages = packages.ToList(),
            PackageCount = packages.Count,
            StatusSummary = SwarmSessionStatusSummaryBuilder.Build(packages, sessionSnapshot.IsPaused)
        };
    }
}
