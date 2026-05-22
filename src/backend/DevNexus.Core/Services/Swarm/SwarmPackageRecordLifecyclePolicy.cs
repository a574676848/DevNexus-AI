using DevNexus.Domain.Entities;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Swarm;

/// <summary>
/// Swarm 工作包记录生命周期策略。
/// </summary>
internal static class SwarmPackageRecordLifecyclePolicy
{
    /// <summary>
    /// 根据当前状态补齐开始、完成和更新时间。
    /// </summary>
    public static void Apply(ContextWorkPackageRecord record, DateTime now)
    {
        record.UpdatedAt = now;

        if (IsStarted(record.Status))
        {
            record.StartedAt ??= now;
        }

        if (IsTerminal(record.Status))
        {
            record.CompletedAt ??= now;
            return;
        }

        record.CompletedAt = null;
    }

    private static bool IsStarted(SwarmTaskStatus status)
    {
        return status is SwarmTaskStatus.InProgress
            or SwarmTaskStatus.GroupChatting
            or SwarmTaskStatus.Evaluating
            or SwarmTaskStatus.Retrying
            or SwarmTaskStatus.Completed
            or SwarmTaskStatus.Failed
            or SwarmTaskStatus.Skipped
            or SwarmTaskStatus.Transferred;
    }

    private static bool IsTerminal(SwarmTaskStatus status)
    {
        return status is SwarmTaskStatus.Completed
            or SwarmTaskStatus.Failed
            or SwarmTaskStatus.Skipped
            or SwarmTaskStatus.Transferred;
    }
}
