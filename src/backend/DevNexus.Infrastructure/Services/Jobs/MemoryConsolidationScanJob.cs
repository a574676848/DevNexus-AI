using DevNexus.Domain.Abstractions;
using Hangfire;
using DevNexus.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Jobs;

/// <summary>
/// 记忆沉淀扫描任务
/// 每日扫描未处理的会话，触发记忆沉淀
/// </summary>
public class MemoryConsolidationScanJob
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IBackgroundJobService _backgroundJobService;
    private readonly ILogger<MemoryConsolidationScanJob> _logger;

    public MemoryConsolidationScanJob(
        ApplicationDbContext dbContext,
        IBackgroundJobService backgroundJobService,
        ILogger<MemoryConsolidationScanJob> logger)
    {
        _dbContext = dbContext;
        _backgroundJobService = backgroundJobService;
        _logger = logger;
    }

    /// <summary>
    /// 执行扫描任务
    /// 扫描条件：
    /// 1. 从未沉淀过 或 上次沉淀超过24小时
    /// 2. 消息数 >= 3
    /// 3. 当前消息数 > 上次沉淀时的消息数
    /// </summary>
    [AutomaticRetry(Attempts = 1, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[MemoryConsolidationScan] Starting daily scan");

        try
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-24);

            // 查询需要沉淀的会话
            var sessionsToProcess = await _dbContext.ChatSessions
                .Include(s => s.Messages)
                .Where(s => s.IsActive)
                .Where(s => s.Messages.Count >= 3) // 至少有3条消息
                .Where(s => 
                    s.LastConsolidatedAt == null || // 从未沉淀
                    s.LastConsolidatedAt < cutoffTime) // 超过24小时未沉淀
                .Where(s => s.Messages.Count > s.LastConsolidatedMessageCount) // 有新消息
                .Select(s => new { s.Id, s.UserId, MessageCount = s.Messages.Count })
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "[MemoryConsolidationScan] Found {Count} sessions to process",
                sessionsToProcess.Count);

            var enqueuedCount = 0;
            foreach (var session in sessionsToProcess)
            {
                try
                {
                    _backgroundJobService.EnqueueMemoryConsolidation(session.Id, session.UserId);
                    enqueuedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "[MemoryConsolidationScan] Failed to enqueue session {SessionId}",
                        session.Id);
                }
            }

            _logger.LogInformation(
                "[MemoryConsolidationScan] Completed - Enqueued={Enqueued} Total={Total}",
                enqueuedCount,
                sessionsToProcess.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MemoryConsolidationScan] Failed");
            throw;
        }
    }
}
