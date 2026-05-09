using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DevNexus.Infrastructure.Repositories;

/// <summary>
/// 客户端更新事件仓储实现。
/// </summary>
public class UpdateClientEventRepository : IUpdateClientEventRepository
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<UpdateClientEventRepository> _logger;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public UpdateClientEventRepository(
        ApplicationDbContext dbContext,
        ILogger<UpdateClientEventRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UpdateClientEvent> AddAsync(UpdateClientEvent clientEvent, CancellationToken cancellationToken = default)
    {
        if (clientEvent.Id == Guid.Empty)
        {
            clientEvent.Id = Guid.NewGuid();
        }

        clientEvent.CreatedAt = DateTime.UtcNow;
        clientEvent.UpdatedAt = DateTime.UtcNow;
        await _dbContext.UpdateClientEvents.AddAsync(clientEvent, cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException postgresException && IsSchemaUnavailable(postgresException))
        {
            _logger.LogWarning(
                ex,
                "[UpdateClientEventRepository] UpdateClientEvents schema unavailable during write. Event will be skipped until migrations repair the schema.");
        }

        return clientEvent;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UpdateClientEvent>> GetSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _dbContext.UpdateClientEvents
                .Where(item => item.CreatedAt >= sinceUtc)
                .OrderByDescending(item => item.CreatedAt)
                .ToListAsync(cancellationToken);
        }
        catch (PostgresException ex) when (IsSchemaUnavailable(ex))
        {
            _logger.LogWarning(
                ex,
                "[UpdateClientEventRepository] UpdateClientEvents schema unavailable during read. Returning an empty event set.");

            return [];
        }
    }

    private static bool IsSchemaUnavailable(PostgresException exception)
    {
        return exception.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.UndefinedColumn;
    }
}
