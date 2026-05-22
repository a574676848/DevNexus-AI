using DevNexus.Domain.Abstractions;
using DevNexus.Shared.DTOs.Swarm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DevNexus.ApiService.Controllers;

/// <summary>
/// Swarm 编排控制器
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class SwarmController : AuthenticatedControllerBase
{
    private readonly IContextSwarmSessionRepository _sessionRepository;
    private readonly DevNexus.Core.Services.Swarm.ISwarmSessionControlService _sessionControlService;
    private readonly ILogger<SwarmController> _logger;

    public SwarmController(
        IContextSwarmSessionRepository sessionRepository,
        DevNexus.Core.Services.Swarm.ISwarmSessionControlService sessionControlService,
        IUserContextAccessor userContextAccessor,
        ILogger<SwarmController> logger)
        : base(userContextAccessor)
    {
        _sessionRepository = sessionRepository;
        _sessionControlService = sessionControlService;
        _logger = logger;
    }

    /// <summary>
    /// 获取当前用户的 Swarm 编排会话历史
    /// </summary>
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();
        var sessions = await _sessionRepository.GetUserSessionsAsync(userId);

        var result = sessions.Select(session =>
        {
            var packages = session.Packages.Select(package => new ContextWorkPackageDto
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
                ExecutorName = package.ExecutorName,
                CommandLine = package.CommandLine,
                WorkingDirectory = package.WorkingDirectory,
                ExecutionReportArtifactId = package.ExecutionReportArtifactId,
                UpdatedAt = package.UpdatedAt,
                CanRetry = package.Status == Shared.Enums.SwarmTaskStatus.Failed,
                OwnedFiles = package.OwnedFiles,
                OwnedSymbols = package.OwnedSymbols
            }).ToList();
            var isPaused = _sessionControlService.GetStatus(session.SessionId) == DevNexus.Core.Services.Swarm.SwarmControlStatus.Paused;

            return new ContextSwarmSessionDto
            {
                SessionId = session.SessionId,
                Title = session.Title,
                Status = session.Status.ToString(),
                Packages = packages,
                StatusSummary = DevNexus.Core.Services.Swarm.SwarmSessionStatusSummaryBuilder.Build(packages, isPaused)
            };
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    /// 中止指定的 Swarm 会话
    /// </summary>
    [HttpPost("sessions/{sessionId}/abort")]
    public async Task<IActionResult> AbortSession(string sessionId)
    {
        var userId = RequireCurrentUserId();
        var session = await _sessionRepository.GetBySessionIdAsync(sessionId);
        if (session == null)
        {
            return NotFound(new { success = false, SessionId = sessionId, message = "Session not found." });
        }

        if (session.UserId != userId)
        {
            return Forbid();
        }

        _logger.LogInformation("HTTP Request to abort Swarm session {SessionId}", sessionId);
        await _sessionControlService.AbortAsync(sessionId);

        return Ok(new { success = true, SessionId = sessionId, message = "Session aborted." });
    }

    /// <summary>
    /// 重试指定会话中的失败工作包。
    /// </summary>
    [HttpPost("sessions/{sessionId}/packages/{packageId}/retry")]
    public async Task<IActionResult> RetryPackage(string sessionId, string packageId, CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();
        var session = await _sessionRepository.GetBySessionIdAsync(sessionId);
        if (session == null)
        {
            return NotFound(new { success = false, SessionId = sessionId, PackageId = packageId, message = "Session not found." });
        }

        if (session.UserId != userId)
        {
            return Forbid();
        }

        _logger.LogInformation("HTTP Request to retry Swarm package {PackageId} in session {SessionId}", packageId, sessionId);
        var command = await _sessionControlService.RetryPackageAsync(sessionId, packageId, cancellationToken);
        if (!command.Accepted)
        {
            return Conflict(new
            {
                success = false,
                SessionId = sessionId,
                PackageId = packageId,
                message = command.Message,
                command
            });
        }

        return Ok(new { success = true, SessionId = sessionId, PackageId = packageId, message = command.Message, command });
    }
}
