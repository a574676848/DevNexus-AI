using DevNexus.Core.Abstractions;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Infrastructure.Models;
using DevNexus.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Repositories;

/// <summary>
/// 终端流仓储实现
/// </summary>
public class TerminalStreamRepository : ITerminalStreamRepository
{
    private const int CommandMaxLength = 2000;
    private const int WorkingDirectoryMaxLength = 1000;
    private const int SessionKeyMaxLength = 200;
    private const int LockKeyMaxLength = 1024;
    private const int StatusMaxLength = 50;
    private const int SessionStateMaxLength = 50;
    private const int RuntimeHostMaxLength = 100;
    private const int TerminationReasonMaxLength = 100;
    private const int ArchivedOutputPathMaxLength = 2000;
    private const int WatchSummaryMaxLength = 1000;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<TerminalStreamRepository> _logger;

    public TerminalStreamRepository(
        ApplicationDbContext dbContext,
        ILogger<TerminalStreamRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TerminalStream?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TerminalStreams
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<TerminalStream>> GetByMessageIdAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TerminalStreams
            .Where(t => t.MessageId == messageId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<TerminalStream>> GetByMessageIdsAsync(
        IReadOnlyCollection<Guid> messageIds,
        CancellationToken cancellationToken = default)
    {
        if (messageIds.Count == 0)
        {
            return new List<TerminalStream>();
        }

        return await _dbContext.TerminalStreams
            .AsNoTracking()
            .Where(stream => stream.MessageId.HasValue && messageIds.Contains(stream.MessageId.Value))
            .OrderBy(stream => stream.MessageId)
            .ThenBy(stream => stream.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<TerminalStream>> GetActiveBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TerminalStreams
            .AsNoTracking()
            .Where(stream => stream.ChatSessionId == sessionId)
            .Where(stream => stream.SessionState == CliSessionState.Created
                || stream.SessionState == CliSessionState.Running
                || stream.SessionState == CliSessionState.WaitingForInput)
            .OrderByDescending(stream => stream.LastActivityAt)
            .ThenByDescending(stream => stream.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TerminalStream?> GetByToolCallIdAsync(Guid toolCallId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.TerminalStreams
            .FirstOrDefaultAsync(t => t.ToolCallId == toolCallId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TerminalStream> CreateAsync(TerminalStream stream, CancellationToken cancellationToken = default)
    {
        if (stream.Id == Guid.Empty)
        {
            stream.Id = Guid.NewGuid();
        }

        NormalizeStreamFields(stream);

        stream.CreatedAt = DateTime.UtcNow;
        stream.UpdatedAt = DateTime.UtcNow;

        await _dbContext.TerminalStreams.AddAsync(stream, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "[TerminalStreamRepository] Created terminal stream | StreamId={StreamId} MessageId={MessageId} Command={Command}",
            stream.Id, stream.MessageId, stream.Command);

        return stream;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(TerminalStream stream, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.TerminalStreams
            .FirstOrDefaultAsync(t => t.Id == stream.Id, cancellationToken);

        if (existing == null)
        {
            _logger.LogWarning(
                "[TerminalStreamRepository] Terminal stream not found for update | StreamId={StreamId}",
                stream.Id);
            throw new InvalidOperationException($"Terminal stream {stream.Id} not found");
        }

        // 更新非主键属性
        existing.Command = stream.Command;
        existing.SessionKey = stream.SessionKey;
        existing.ChatSessionId = stream.ChatSessionId;
        existing.UserId = stream.UserId;
        existing.WorkingDirectory = stream.WorkingDirectory;
        existing.LockKey = stream.LockKey;
        existing.AttemptNumber = stream.AttemptNumber;
        existing.IsRetry = stream.IsRetry;
        existing.Status = stream.Status;
        existing.SessionState = stream.SessionState;
        existing.RuntimeHost = stream.RuntimeHost;
        existing.ExitCode = stream.ExitCode;
        existing.StartedAt = stream.StartedAt;
        existing.LastActivityAt = stream.LastActivityAt;
        existing.WaitingForInput = stream.WaitingForInput;
        existing.WaitingForInputSince = stream.WaitingForInputSince;
        existing.TerminationReason = stream.TerminationReason;
        existing.Output = stream.Output;
        existing.HasArchivedOutput = stream.HasArchivedOutput;
        existing.ArchivedOutputPath = stream.ArchivedOutputPath;
        existing.OutputLength = stream.OutputLength;
        existing.OutputLineCount = stream.OutputLineCount;
        existing.OutputChunkCount = stream.OutputChunkCount;
        existing.WatchSummary = stream.WatchSummary;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = stream.UpdatedBy;

        NormalizeStreamFields(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "[TerminalStreamRepository] Updated terminal stream | StreamId={StreamId} Status={Status} OutputLength={Length}",
            stream.Id, stream.Status, stream.Output?.Length ?? 0);
    }

    /// <inheritdoc />
    public async Task<List<TerminalStream>> CreateBatchAsync(List<TerminalStream> streams, CancellationToken cancellationToken = default)
    {
        if (streams == null || streams.Count == 0)
        {
            return new List<TerminalStream>();
        }

        var now = DateTime.UtcNow;
        foreach (var stream in streams)
        {
            if (stream.Id == Guid.Empty)
            {
                stream.Id = Guid.NewGuid();
            }

            NormalizeStreamFields(stream);
            stream.CreatedAt = now;
            stream.UpdatedAt = now;
        }

        await _dbContext.TerminalStreams.AddRangeAsync(streams, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "[TerminalStreamRepository] Created {Count} terminal streams in batch",
            streams.Count);

        return streams;
    }

    private static void NormalizeStreamFields(TerminalStream stream)
    {
        stream.Command = Truncate(stream.Command, CommandMaxLength) ?? string.Empty;
        stream.WorkingDirectory = Truncate(stream.WorkingDirectory, WorkingDirectoryMaxLength);
        stream.SessionKey = Truncate(stream.SessionKey, SessionKeyMaxLength);
        stream.LockKey = Truncate(stream.LockKey, LockKeyMaxLength);
        stream.RuntimeHost = Truncate(stream.RuntimeHost, RuntimeHostMaxLength);
        stream.TerminationReason = Truncate(stream.TerminationReason, TerminationReasonMaxLength);
        stream.ArchivedOutputPath = Truncate(stream.ArchivedOutputPath, ArchivedOutputPathMaxLength);
        stream.WatchSummary = Truncate(stream.WatchSummary, WatchSummaryMaxLength);
        stream.Output ??= string.Empty;
        stream.OutputLength = Math.Max(stream.OutputLength, stream.Output.Length);
        stream.OutputLineCount = Math.Max(stream.OutputLineCount, string.IsNullOrEmpty(stream.Output) ? 0 : stream.Output.Count(c => c == '\n') + 1);
        stream.OutputChunkCount = Math.Max(stream.OutputChunkCount, 0);

        if (stream.Status == TerminalStreamStatus.Unknown)
        {
            stream.Status = TerminalStreamStatus.Running;
        }

        if (stream.SessionState == CliSessionState.Unknown)
        {
            stream.SessionState = CliSessionState.Created;
        }
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
