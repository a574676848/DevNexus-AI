using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Swarm;

/// <summary>
/// Swarm 控制命令策略。
/// </summary>
public static class SwarmControlCommandPolicy
{
    /// <summary>
    /// 判断是否允许暂停会话。
    /// </summary>
    public static SwarmControlDecision CanPause(SwarmStatus? status)
    {
        if (status == null)
        {
            return SwarmControlDecision.Reject("PauseRejected", "Swarm 会话不存在，无法暂停。");
        }

        return IsTerminalStatus(status.Value)
            ? SwarmControlDecision.Reject("PauseRejected", "Swarm 已经结束，无法暂停。")
            : SwarmControlDecision.Accept("Paused");
    }

    /// <summary>
    /// 判断是否允许继续会话。
    /// </summary>
    public static SwarmControlDecision CanResume(SwarmStatus? status)
    {
        if (status == null)
        {
            return SwarmControlDecision.Reject("ResumeRejected", "Swarm 会话不存在，无法继续。");
        }

        return IsTerminalStatus(status.Value)
            ? SwarmControlDecision.Reject("ResumeRejected", "Swarm 已经结束，无法继续。")
            : SwarmControlDecision.Accept("Resumed");
    }

    /// <summary>
    /// 判断是否允许重试工作包。
    /// </summary>
    public static SwarmControlDecision CanRetryPackage(SwarmStatus? sessionStatus, SwarmTaskStatus? packageStatus)
    {
        if (sessionStatus == null)
        {
            return SwarmControlDecision.Reject("RetryRejected", "Swarm 会话不存在，无法重试工作包。");
        }

        if (sessionStatus == SwarmStatus.Aborted)
        {
            return SwarmControlDecision.Reject("RetryRejected", "Swarm 已中止，无法重试工作包。");
        }

        if (sessionStatus == SwarmStatus.Completed)
        {
            return SwarmControlDecision.Reject("RetryRejected", "Swarm 已完成，无法重试工作包。");
        }

        if (packageStatus == null)
        {
            return SwarmControlDecision.Reject("RetryRejected", "工作包不存在，无法重试。");
        }

        return packageStatus.Value == SwarmTaskStatus.Failed
            ? SwarmControlDecision.Accept("RetryStarted")
            : SwarmControlDecision.Reject("RetryRejected", "仅允许重试失败工作包。");
    }

    private static bool IsTerminalStatus(SwarmStatus status)
    {
        return status is SwarmStatus.Completed or SwarmStatus.Failed or SwarmStatus.Aborted;
    }
}

/// <summary>
/// Swarm 控制命令裁决。
/// </summary>
public sealed record SwarmControlDecision(
    bool Accepted,
    string Command,
    string Message)
{
    /// <summary>
    /// 构建接受裁决。
    /// </summary>
    public static SwarmControlDecision Accept(string command)
    {
        return new SwarmControlDecision(true, command, string.Empty);
    }

    /// <summary>
    /// 构建拒绝裁决。
    /// </summary>
    public static SwarmControlDecision Reject(string command, string message)
    {
        return new SwarmControlDecision(false, command, message);
    }
}
