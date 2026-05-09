// using DevNexus.Domain.Abstractions via GlobalUsings
using DevNexus.Infrastructure.Services.Jobs;
using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Jobs;

/// <summary>
/// 后台任务服务实现
/// 基于 Hangfire 实现异步任务队列
/// </summary>
public class BackgroundJobService : IBackgroundJobService
{
    private static readonly string[] LegacyRecurringJobIdFragments =
    [
        "workspace-cleanup",
        "user-workspace-cleanup"
    ];

    private static readonly string[] LegacyRecurringJobTypeNames =
    [
        "DevNexus.Infrastructure.Services.Jobs.UserWorkspaceCleanupJob",
        "UserWorkspaceCleanupJob"
    ];

    private readonly ILogger<BackgroundJobService> _logger;
    private readonly IBackgroundJobClient _jobClient;
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly JobStorage _jobStorage;
    
    public BackgroundJobService(
        ILogger<BackgroundJobService> logger,
        IBackgroundJobClient jobClient,
        IRecurringJobManager recurringJobManager,
        JobStorage jobStorage)
    {
        _logger = logger;
        _jobClient = jobClient;
        _recurringJobManager = recurringJobManager;
        _jobStorage = jobStorage;
    }
    
    /// <inheritdoc />
    public string EnqueueCleanupExpiredFiles(int daysOld = 30)
    {
        var jobId = _jobClient.Enqueue<CleanupJob>(
            job => job.CleanupExpiredFilesAsync(daysOld, CancellationToken.None));
        
        _logger.LogInformation(
            "[BackgroundJob.Enqueue] Cleanup job enqueued | JobId={JobId} DaysOld={DaysOld}",
            jobId,
            daysOld);
        
        return jobId;
    }
    
    /// <inheritdoc />
    public void ScheduleDailyCleanup()
    {
        // 每天凌晨 2:00 执行清理任务
        _recurringJobManager.AddOrUpdate<CleanupJob>(
            "daily-cleanup",
            job => job.CleanupExpiredFilesAsync(30, CancellationToken.None),
            Cron.Daily(2));
        
        _logger.LogInformation("[BackgroundJob.Schedule] Daily cleanup job scheduled");
    }
    
    /// <inheritdoc />
    public void ScheduleSessionCleanup()
    {
        // 每天凌晨 3:00 清理超过 90 天不活跃的会话
        _recurringJobManager.AddOrUpdate<SessionCleanupJob>(
            "daily-session-cleanup",
            job => job.CleanupInactiveSessionsAsync(90, CancellationToken.None),
            Cron.Daily(3));
        
        _logger.LogInformation("[BackgroundJob.Schedule] Daily session cleanup job scheduled");
    }
    
    /// <inheritdoc />
    public void ScheduleStuckMessagesCleanup()
    {
        // 每小时清理卡住的消息
        _recurringJobManager.AddOrUpdate<SessionCleanupJob>(
            "hourly-stuck-messages-cleanup",
            job => job.CleanupStuckMessagesAsync(CancellationToken.None),
            Cron.Hourly());
        
        _logger.LogInformation("[BackgroundJob.Schedule] Hourly stuck messages cleanup job scheduled");
    }

    /// <inheritdoc />
    public void CleanupLegacyRecurringJobs()
    {
        try
        {
            using var connection = _jobStorage.GetConnection();
            var recurringJobIds = connection.GetAllItemsFromSet("recurring-jobs");
            if (recurringJobIds == null || recurringJobIds.Count == 0)
            {
                return;
            }

            var removedJobIds = new List<string>();
            foreach (var recurringJobId in recurringJobIds)
            {
                if (!ShouldRemoveLegacyRecurringJob(connection, recurringJobId))
                {
                    continue;
                }

                _recurringJobManager.RemoveIfExists(recurringJobId);
                removedJobIds.Add(recurringJobId);
            }

            if (removedJobIds.Count == 0)
            {
                return;
            }

            _logger.LogInformation(
                "[BackgroundJob.Schedule] 已清理失效的历史循环任务 | JobIds={JobIds}",
                string.Join(", ", removedJobIds));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[BackgroundJob.Schedule] 清理历史循环任务失败，继续注册当前任务。");
        }
    }
    
    /// <inheritdoc />
    public bool DeleteJob(string jobId)
    {
        try
        {
            var result = _jobClient.Delete(jobId);
            
            _logger.LogInformation(
                "[BackgroundJob.Delete] Job deleted | JobId={JobId} Success={Success}",
                jobId,
                result);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[BackgroundJob.Delete] Failed to delete job | JobId={JobId}",
                jobId);
            return false;
        }
    }
    
    /// <inheritdoc />
    public string ScheduleMemoryConsolidation(Guid sessionId, Guid userId, TimeSpan delay)
    {
        var jobId = _jobClient.Schedule<MemoryConsolidationJob>(
            job => job.ExecuteAsync(sessionId, userId, CancellationToken.None),
            delay);
        
        _logger.LogInformation(
            "[BackgroundJob.Schedule] Memory consolidation scheduled | JobId={JobId} SessionId={SessionId} Delay={Delay}",
            jobId,
            sessionId,
            delay);
        
        return jobId;
    }
    
    /// <inheritdoc />
    public string EnqueueMemoryConsolidation(Guid sessionId, Guid userId)
    {
        var jobId = _jobClient.Enqueue<MemoryConsolidationJob>(
            job => job.ExecuteAsync(sessionId, userId, CancellationToken.None));
        
        _logger.LogInformation(
            "[BackgroundJob.Enqueue] Memory consolidation enqueued | JobId={JobId} SessionId={SessionId}",
            jobId,
            sessionId);
        
        return jobId;
    }
    
    /// <inheritdoc />
    public bool CancelMemoryConsolidation(string jobId)
    {
        if (string.IsNullOrEmpty(jobId))
            return false;
            
        try
        {
            var result = _jobClient.Delete(jobId);
            
            _logger.LogInformation(
                "[BackgroundJob.Cancel] Memory consolidation cancelled | JobId={JobId} Success={Success}",
                jobId,
                result);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[BackgroundJob.Cancel] Failed to cancel memory consolidation | JobId={JobId}",
                jobId);
            return false;
        }
    }
    
    /// <inheritdoc />
    public void ScheduleDailyMemoryConsolidationScan()
    {
        // 每天凌晨 4:00 执行记忆沉淀扫描
        _recurringJobManager.AddOrUpdate<MemoryConsolidationScanJob>(
            "daily-memory-consolidation-scan",
            job => job.ExecuteAsync(CancellationToken.None),
            Cron.Daily(4));
        
        _logger.LogInformation("[BackgroundJob.Schedule] Daily memory consolidation scan scheduled at 4:00 AM");
    }

    /// <inheritdoc />
    public string ScheduleExperienceDistillation(Guid sessionId, TimeSpan delay)
    {
        var jobId = _jobClient.Schedule<ExperienceDistillationJob>(
            job => job.DistillSessionAsync(sessionId, CancellationToken.None),
            delay);
        
        _logger.LogInformation("[BackgroundJob] 预定经验提纯任务，Delay = {Delay}", delay);
        return jobId;
    }

    /// <inheritdoc />
    public void ScheduleDailyExperiencePruning()
    {
        _recurringJobManager.AddOrUpdate<ExperiencePruningJob>(
            "daily-experience-pruning",
            job => job.PruneAsync(CancellationToken.None),
            Cron.Daily(5)); // 凌晨 5 点
        
        _logger.LogInformation("[BackgroundJob] 注册每日经验修剪任务");
    }

    private static bool ShouldRemoveLegacyRecurringJob(IStorageConnection connection, string recurringJobId)
    {
        if (LegacyRecurringJobIdFragments.Any(fragment =>
                recurringJobId.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var recurringJobMetadata = connection.GetAllEntriesFromHash($"recurring-job:{recurringJobId}");
        if (recurringJobMetadata == null || recurringJobMetadata.Count == 0)
        {
            return false;
        }

        return recurringJobMetadata.Values.Any(value =>
            !string.IsNullOrWhiteSpace(value)
            && LegacyRecurringJobTypeNames.Any(typeName =>
                value.Contains(typeName, StringComparison.OrdinalIgnoreCase)));
    }
}
