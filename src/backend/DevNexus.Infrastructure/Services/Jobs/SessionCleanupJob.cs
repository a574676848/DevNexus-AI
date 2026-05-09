// using DevNexus.Domain.Abstractions via GlobalUsings
// using DevNexus.Domain.Entities via GlobalUsings
using DevNexus.Shared.Constants;
using DevNexus.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Jobs;

/// <summary>
/// 会话清理任务
/// 负责清理长时间不活跃的会话状态和数据
/// </summary>
public class SessionCleanupJob
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IElasticsearchSearchService? _searchService;
    private readonly ILogger<SessionCleanupJob> _logger;

    public SessionCleanupJob(
        ApplicationDbContext dbContext,
        ILogger<SessionCleanupJob> logger,
        IElasticsearchSearchService? searchService = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        _searchService = searchService;
    }

    /// <summary>
    /// 清理不活跃的会话（软删除）
    /// 将超过指定天数未更新的会话标记为 IsActive = false
    /// </summary>
    /// <param name="inactiveDays">不活跃天数阈值</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task CleanupInactiveSessionsAsync(
        int inactiveDays,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug(
                "[Job.SessionCleanup] Starting | InactiveDays={InactiveDays}",
                inactiveDays);

            var cutoffDate = DateTime.UtcNow.AddDays(-inactiveDays);
            var deactivatedCount = 0;

            // 查询超过指定天数未更新且仍为活跃状态的会话
            var inactiveSessions = await _dbContext.ChatSessions
                .Where(s => s.IsActive && s.UpdatedAt < cutoffDate)
                .ToListAsync(cancellationToken);

            _logger.LogDebug(
                "[Job.SessionCleanup] Found {Count} inactive sessions older than {Days} days",
                inactiveSessions.Count,
                inactiveDays);

            // 批量标记为不活跃
            foreach (var session in inactiveSessions)
            {
                session.IsActive = false;
                session.UpdatedAt = DateTime.UtcNow;
                deactivatedCount++;
            }

            // 保存更改
            if (inactiveSessions.Count > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogDebug(
                "[Job.SessionCleanup] Completed | Deactivated={Count}",
                deactivatedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[Job.SessionCleanup] Failed | InactiveDays={InactiveDays}",
                inactiveDays);
            throw;
        }
    }

    /// <summary>
    /// 清理「生成中」卡住的消息状态
    /// 将超过 1 小时仍处于 in_progress 状态的消息标记为 error
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task CleanupStuckMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("[Job.SessionCleanup] Cleaning stuck messages");

            var cutoffTime = DateTime.UtcNow.AddHours(-1);
            var cleanedCount = 0;

            // 查询超过 1 小时仍处于 in_progress 状态的消息
            var stuckMessages = await _dbContext.ChatMessages
                .Where(m => m.Status == ChatConstants.StatusInProgress && m.CreatedAt < cutoffTime)
                .ToListAsync(cancellationToken);

            // 标记为 error
            foreach (var message in stuckMessages)
            {
                message.Status = ChatConstants.StatusError;
                message.Content = new Dictionary<string, object>
                {
                    { "text", "⚠️ 消息生成超时，请重试" }
                };
                message.UpdatedAt = DateTime.UtcNow;
                cleanedCount++;
            }

            if (stuckMessages.Count > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogDebug(
                "[Job.SessionCleanup] Cleaned {Count} stuck messages",
                cleanedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Job.SessionCleanup] Failed to clean stuck messages");
            throw;
        }
    }

    /// <summary>
    /// 物理删除长期不活跃的会话及其消息（可选，谨慎使用）
    /// 同时清理 Elasticsearch 中的索引数据
    /// </summary>
    /// <param name="inactiveDays">不活跃天数阈值（建议 180 天以上）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task PurgeOldSessionsAsync(
        int inactiveDays,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug(
                "[Job.SessionPurge] Starting | InactiveDays={InactiveDays}",
                inactiveDays);

            var cutoffDate = DateTime.UtcNow.AddDays(-inactiveDays);

            // 查询超过指定天数未更新且已被标记为不活跃的会话
            var oldSessions = await _dbContext.ChatSessions
                .Where(s => !s.IsActive && s.UpdatedAt < cutoffDate)
                .ToListAsync(cancellationToken);

            _logger.LogDebug(
                "[Job.SessionPurge] Found {Count} old sessions to purge",
                oldSessions.Count);

            var esCleanedCount = 0;
            var esErrorCount = 0;

            // 先清理 Elasticsearch 索引（在删除数据库记录之前）
            if (_searchService != null)
            {
                foreach (var session in oldSessions)
                {
                    try
                    {
                        // 删除会话索引
                        await _searchService.DeleteSessionAsync(
                            session.Id.ToString(),
                            cancellationToken);
                        
                        // 删除会话下的所有消息索引
                        await _searchService.DeleteSessionMessagesAsync(
                            session.Id.ToString(),
                            cancellationToken);
                        
                        esCleanedCount++;
                    }
                    catch (Exception ex)
                    {
                        // ES 清理失败不阻止数据库删除，仅记录警告
                        _logger.LogWarning(
                            ex,
                            "[Job.SessionPurge] Failed to clean ES data for session {SessionId}",
                            session.Id);
                        esErrorCount++;
                    }
                }
                
                _logger.LogDebug(
                    "[Job.SessionPurge] ES cleanup completed | Cleaned={Cleaned} Errors={Errors}",
                    esCleanedCount,
                    esErrorCount);
            }

            // 从数据库批量删除（级联删除消息）
            _dbContext.ChatSessions.RemoveRange(oldSessions);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug(
                "[Job.SessionPurge] Completed | DBPurged={Count} ESCleaned={ESCleaned}",
                oldSessions.Count,
                esCleanedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[Job.SessionPurge] Failed | InactiveDays={InactiveDays}",
                inactiveDays);
            throw;
        }
    }
}
