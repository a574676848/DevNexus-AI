using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Core.Services.Swarm.Planning;
using DevNexus.Infrastructure.Models;
using DevNexus.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Repositories;

/// <summary>
/// 上下文驱动 Swarm 会话持久化仓库实现
/// </summary>
public class ContextSwarmSessionRepository : IContextSwarmSessionRepository
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<ContextSwarmSessionRepository> _logger;

    public ContextSwarmSessionRepository(ApplicationDbContext dbContext, ILogger<ContextSwarmSessionRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ContextSwarmSession?> GetBySessionIdAsync(string sessionId)
    {
        return await _dbContext.ContextSwarmSessions
            .Include(s => s.Packages)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);
    }

    public async Task SaveAsync(ContextSwarmSession session)
    {
        var existing = await _dbContext.ContextSwarmSessions.FirstOrDefaultAsync(s => s.SessionId == session.SessionId);
        if (existing == null)
        {
            _dbContext.ContextSwarmSessions.Add(session);
        }
        else
        {
            // ★ 只更新非主键属性，避免 EF Core 主键冲突异常
            // SetValues() 会尝试复制所有属性（包括主键 Id），这是禁止的
            existing.Title = session.Title;
            existing.Description = session.Description;
            existing.Status = session.Status;
            existing.StartedAt = session.StartedAt;
            existing.CompletedAt = session.CompletedAt;
            existing.Result = session.Result;
            existing.DomainType = session.DomainType;
            existing.ProviderId = session.ProviderId;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = session.UpdatedBy;
            
            // 合并 Packages 集合（保持已有的，添加新的）
            if (session.Packages?.Any() == true)
            {
                foreach (var newTask in session.Packages)
                {
                    var existingTask = existing.Packages.FirstOrDefault(t => t.TaskId == newTask.TaskId);
                    if (existingTask == null)
                    {
                        existing.Packages.Add(newTask);
                    }
                    else
                    {
                        // 更新已存在的任务属性
                        existingTask.Title = newTask.Title;
                        existingTask.Description = newTask.Description;
                        existingTask.Role = newTask.Role;
                        existingTask.ContextType = newTask.ContextType;
                        existingTask.ExecutionStrategy = newTask.ExecutionStrategy;
                        existingTask.Status = newTask.Status;
                        existingTask.Dependencies = newTask.Dependencies;
                        existingTask.Result = newTask.Result;
                        existingTask.FailureReason = newTask.FailureReason;
                        existingTask.ExecutorName = newTask.ExecutorName;
                        existingTask.CommandLine = newTask.CommandLine;
                        existingTask.WorkingDirectory = newTask.WorkingDirectory;
                        existingTask.ExecutionReportArtifactId = newTask.ExecutionReportArtifactId;
                        existingTask.StartedAt = newTask.StartedAt;
                        existingTask.CompletedAt = newTask.CompletedAt;
                        existingTask.LogicalUnits = newTask.LogicalUnits;
                        existingTask.InputContracts = newTask.InputContracts;
                        existingTask.OutputContracts = newTask.OutputContracts;
                        existingTask.OwnedFiles = newTask.OwnedFiles;
                        existingTask.OwnedSymbols = newTask.OwnedSymbols;
                        existingTask.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }
            
            _dbContext.ContextSwarmSessions.Update(existing);
        }
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateTaskAsync(string sessionId, ContextWorkPackageRecord task)
    {
        var session = await _dbContext.ContextSwarmSessions
            .Include(s => s.Packages)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);

        if (session == null)
        {
            _logger.LogWarning("Cannot update task: Swarm session {SessionId} not found.", sessionId);
            return;
        }

        var existingTask = session.Packages.FirstOrDefault(t => string.Equals(t.TaskId, task.TaskId, StringComparison.Ordinal));
        if (existingTask != null)
        {
            existingTask.Status = task.Status;
            existingTask.ContextType = task.ContextType;
            existingTask.ExecutionStrategy = task.ExecutionStrategy;
            existingTask.Result = task.Result;
            existingTask.FailureReason = task.FailureReason;
            existingTask.ExecutorName = task.ExecutorName;
            existingTask.CommandLine = task.CommandLine;
            existingTask.WorkingDirectory = task.WorkingDirectory;
            existingTask.ExecutionReportArtifactId = task.ExecutionReportArtifactId;
            existingTask.StartedAt = task.StartedAt;
            existingTask.CompletedAt = task.CompletedAt;
            existingTask.Dependencies = task.Dependencies;
            existingTask.OwnedFiles = task.OwnedFiles;
            existingTask.OwnedSymbols = task.OwnedSymbols;
            existingTask.LogicalUnits = task.LogicalUnits;
            existingTask.InputContracts = task.InputContracts;
            existingTask.OutputContracts = task.OutputContracts;
        }
        else
        {
            session.Packages.Add(new ContextWorkPackageRecord
            {
                TaskId = task.TaskId,
                Title = task.Title,
                Description = task.Description,
                Role = task.Role,
                ContextType = task.ContextType,
                ExecutionStrategy = task.ExecutionStrategy,
                Status = task.Status,
                Dependencies = task.Dependencies,
                Result = task.Result,
                FailureReason = task.FailureReason,
                ExecutorName = task.ExecutorName,
                CommandLine = task.CommandLine,
                WorkingDirectory = task.WorkingDirectory,
                ExecutionReportArtifactId = task.ExecutionReportArtifactId,
                StartedAt = task.StartedAt,
                CompletedAt = task.CompletedAt,
                LogicalUnits = task.LogicalUnits,
                InputContracts = task.InputContracts,
                OutputContracts = task.OutputContracts,
                OwnedFiles = task.OwnedFiles,
                OwnedSymbols = task.OwnedSymbols
            });
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task<List<ContextSwarmSession>> GetUserSessionsAsync(Guid userId)
    {
        return await _dbContext.ContextSwarmSessions
            .AsQueryable()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync();
    }

    public async Task<List<ContextSwarmSession>> ListByExternalSessionIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ContextSwarmSessions
            .Where(session => session.SessionId == sessionId)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteRangeAsync(IReadOnlyCollection<ContextSwarmSession> sessions, CancellationToken cancellationToken = default)
    {
        if (sessions.Count == 0)
        {
            return;
        }

        _dbContext.ContextSwarmSessions.RemoveRange(sessions);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateSessionStatusAsync(string sessionId, SwarmStatus status, string? result = null)
    {
        var session = await _dbContext.ContextSwarmSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId);
        if (session != null)
        {
            session.Status = status;
            if (result != null) session.Result = result;
            if (status == SwarmStatus.Completed || status == SwarmStatus.Failed)
            {
                session.CompletedAt = DateTime.UtcNow;
            }
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task<List<ContextSwarmSession>> GetInterruptedSessionsAsync()
    {
        return await _dbContext.ContextSwarmSessions
            .Include(s => s.Packages)
            .Where(s => s.Status == SwarmStatus.Running)
            .OrderBy(s => s.StartedAt)
            .ToListAsync();
    }
}
