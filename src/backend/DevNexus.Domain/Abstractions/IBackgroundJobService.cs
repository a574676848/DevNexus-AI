using DevNexus.Domain.Models;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 后台任务服务接口
/// 处理异步任务如数据清理、会话管理等
/// </summary>
public interface IBackgroundJobService
{
    /// <summary>
    /// 清理过期文件
    /// </summary>
    /// <param name="daysOld">保留天数</param>
    /// <returns>任务ID</returns>
    string EnqueueCleanupExpiredFiles(int daysOld = 30);
    
    /// <summary>
    /// 定期清理过期文件（每天执行）
    /// </summary>
    void ScheduleDailyCleanup();
    
    /// <summary>
    /// 定期清理不活跃会话（每天执行）
    /// </summary>
    void ScheduleSessionCleanup();
    
    /// <summary>
    /// 定期清理卡住的消息（每小时执行）
    /// </summary>
    void ScheduleStuckMessagesCleanup();

    /// <summary>
    /// 清理 Hangfire 中已失效的历史循环任务定义。
    /// </summary>
    void CleanupLegacyRecurringJobs();
    
    /// <summary>
    /// 删除后台任务
    /// </summary>
    /// <param name="jobId">任务ID</param>
    /// <returns>是否成功</returns>
    bool DeleteJob(string jobId);
    
    /// <summary>
    /// 调度延迟的记忆沉淀任务（30分钟后执行）
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="userId">用户ID</param>
    /// <param name="delay">延迟时间</param>
    /// <returns>任务ID</returns>
    string ScheduleMemoryConsolidation(Guid sessionId, Guid userId, TimeSpan delay);
    
    /// <summary>
    /// 立即入队记忆沉淀任务
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="userId">用户ID</param>
    /// <returns>任务ID</returns>
    string EnqueueMemoryConsolidation(Guid sessionId, Guid userId);
    
    /// <summary>
    /// 取消已调度的记忆沉淀任务
    /// </summary>
    /// <param name="jobId">任务ID</param>
    /// <returns>是否成功取消</returns>
    bool CancelMemoryConsolidation(string jobId);
    
    /// <summary>
    /// 注册每日记忆沉淀扫描任务（凌晨4:00执行）
    /// </summary>
    void ScheduleDailyMemoryConsolidationScan();

    /// <summary>
    /// 触发会话经验提纯（对话拦截或 Swarm 结束后）
    /// </summary>
    /// <param name="sessionId">会话 ID。</param>
    /// <param name="delay">延迟时间。</param>
    /// <param name="scheduleContext">调度上下文。</param>
    string ScheduleExperienceDistillation(
        Guid sessionId,
        TimeSpan delay,
        ExperienceDistillationScheduleContext? scheduleContext = null);

    /// <summary>
    /// 每日系统经验修剪（低分清理与效用衰减）
    /// </summary>
    void ScheduleDailyExperiencePruning();
}
