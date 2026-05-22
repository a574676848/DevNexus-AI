using DevNexus.Shared.DTOs.Swarm;

namespace DevNexus.Core.Services.Swarm;

/// <summary>
/// Swarm 会话状态摘要构建器。
/// </summary>
public static class SwarmSessionStatusSummaryBuilder
{
    private const string NeutralTone = "neutral";
    private const string InfoTone = "info";
    private const string ActiveTone = "active";
    private const string WarningTone = "warning";
    private const string SuccessTone = "success";
    private const string DangerTone = "danger";

    /// <summary>
    /// 基于工作包快照构建会话状态摘要。
    /// </summary>
    public static SwarmSessionStatusSummaryDto Build(
        IReadOnlyCollection<ContextWorkPackageDto> packages,
        bool isPaused)
    {
        var totalCount = packages.Count;
        var failedCount = packages.Count(package => IsStatus(package.Status, SwarmTaskStatusNames.Failed));
        var planningCount = packages.Count(package => IsPlanningStatus(package.Status));
        var executingCount = packages.Count(package => IsExecutingStatus(package.Status));
        var evaluatingCount = packages.Count(package => IsEvaluatingStatus(package.Status));
        var terminalCount = packages.Count(package => IsTerminalStatus(package.Status));
        var isTerminal = totalCount > 0 && terminalCount == totalCount;
        var phase = ResolvePhase(totalCount, failedCount, isPaused, executingCount, evaluatingCount, isTerminal);

        return new SwarmSessionStatusSummaryDto
        {
            Tone = phase.Tone,
            Label = phase.Label,
            Description = phase.Description,
            TotalCount = totalCount,
            PlanningCount = planningCount,
            ExecutingCount = executingCount,
            EvaluatingCount = evaluatingCount,
            TerminalCount = terminalCount,
            FailedCount = failedCount,
            HasFailures = failedCount > 0,
            IsPaused = isPaused,
            IsTerminal = isTerminal,
            StageMetrics = BuildStageMetrics(phase.Tone, totalCount, planningCount, executingCount, evaluatingCount, terminalCount)
        };
    }

    private static SwarmPhase ResolvePhase(
        int totalCount,
        int failedCount,
        bool isPaused,
        int executingCount,
        int evaluatingCount,
        bool isTerminal)
    {
        if (totalCount == 0)
        {
            return new SwarmPhase(NeutralTone, "等待工作包进入 Swarm", "等待系统拆解工作包并生成执行计划。");
        }

        if (failedCount > 0)
        {
            return new SwarmPhase(DangerTone, "存在失败工作包", "存在异常工作包，建议优先查看时间轴和工作包详情中的失败原因。");
        }

        if (isPaused)
        {
            return new SwarmPhase(WarningTone, "Swarm 已暂停", "当前执行流已暂停，节点会保留在当前位置，等待继续指令。");
        }

        if (executingCount > 0)
        {
            return new SwarmPhase(ActiveTone, "Swarm 正在执行", "当前正在推进工作包执行链路，相关路径会被自动强调。");
        }

        if (evaluatingCount > 0)
        {
            return new SwarmPhase(WarningTone, "Swarm 正在评估", "部分工作包正在评估或重试，属于正常收尾阶段。");
        }

        if (isTerminal)
        {
            return new SwarmPhase(SuccessTone, "Swarm 已收尾", "所有工作包已经结束，当前视图主要用于复盘结果。");
        }

        return new SwarmPhase(InfoTone, "Swarm 正在规划", "任务已进入编排阶段，正在等待后续执行。");
    }

    private static List<SwarmStageMetricDto> BuildStageMetrics(
        string tone,
        int totalCount,
        int planningCount,
        int executingCount,
        int evaluatingCount,
        int terminalCount)
    {
        return new List<SwarmStageMetricDto>
        {
            new()
            {
                Label = "规划中",
                Count = planningCount,
                Active = tone == InfoTone || (totalCount > 0 && planningCount > 0 && executingCount == 0 && evaluatingCount == 0 && terminalCount == 0),
                Tone = InfoTone
            },
            new()
            {
                Label = "执行中",
                Count = executingCount,
                Active = tone == ActiveTone,
                Tone = ActiveTone
            },
            new()
            {
                Label = "评估中",
                Count = evaluatingCount,
                Active = tone == WarningTone || (totalCount > 0 && evaluatingCount > 0 && executingCount == 0),
                Tone = WarningTone
            },
            new()
            {
                Label = "收尾中",
                Count = terminalCount,
                Active = tone == SuccessTone,
                Tone = SuccessTone
            }
        };
    }

    private static bool IsPlanningStatus(string? status)
    {
        return IsStatus(status, SwarmTaskStatusNames.Pending) || IsStatus(status, "Ready");
    }

    private static bool IsExecutingStatus(string? status)
    {
        return IsStatus(status, SwarmTaskStatusNames.InProgress) || IsStatus(status, SwarmTaskStatusNames.GroupChatting);
    }

    private static bool IsEvaluatingStatus(string? status)
    {
        return IsStatus(status, SwarmTaskStatusNames.Evaluating) || IsStatus(status, SwarmTaskStatusNames.Retrying);
    }

    private static bool IsTerminalStatus(string? status)
    {
        return IsStatus(status, SwarmTaskStatusNames.Completed)
            || IsStatus(status, SwarmTaskStatusNames.Failed)
            || IsStatus(status, SwarmTaskStatusNames.Skipped)
            || IsStatus(status, SwarmTaskStatusNames.Transferred);
    }

    private static bool IsStatus(string? status, string expected)
    {
        return string.Equals(status, expected, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record SwarmPhase(string Tone, string Label, string Description);
}
