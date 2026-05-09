using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevNexus.Infrastructure.Repositories;

/// <summary>
/// CLI 执行会话仓储实现。
/// </summary>
public class CliExecSessionRepository : ICliExecSessionRepository
{
    private readonly ApplicationDbContext _dbContext;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public CliExecSessionRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<CliExecSession?> GetBySessionKeyAsync(string sessionKey, CancellationToken cancellationToken = default)
    {
        return _dbContext.Set<CliExecSession>()
            .FirstOrDefaultAsync(session => session.SessionKey == sessionKey, cancellationToken);
    }

    /// <inheritdoc />
    public Task<CliExecSession?> GetLatestByChatSessionIdAsync(Guid chatSessionId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Set<CliExecSession>()
            .Where(session => session.ChatSessionId == chatSessionId)
            .OrderByDescending(session => session.LastActivityAt ?? session.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpsertAsync(CliExecSession session, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Set<CliExecSession>()
            .FirstOrDefaultAsync(item => item.SessionKey == session.SessionKey, cancellationToken);

        if (existing == null)
        {
            await _dbContext.Set<CliExecSession>().AddAsync(session, cancellationToken);
        }
        else
        {
            existing.ChatSessionId = session.ChatSessionId;
            existing.UserId = session.UserId;
            existing.ExecStatus = session.ExecStatus;
            existing.SessionMode = session.SessionMode;
            existing.Command = session.Command;
            existing.WorkingDirectory = session.WorkingDirectory;
            existing.RuntimeHost = session.RuntimeHost;
            existing.TerminalStreamId = session.TerminalStreamId ?? existing.TerminalStreamId;
            existing.StartedAt = session.StartedAt;
            existing.LastActivityAt = session.LastActivityAt;
            existing.WaitingForInput = session.WaitingForInput;
            existing.WaitingForInputSince = session.WaitingForInputSince;
            existing.ExitCode = session.ExitCode;
            existing.TerminationReason = session.TerminationReason;
            existing.IsActive = session.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
