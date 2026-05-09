using DevNexus.Shared.DTOs.Swarm;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using System.Text.Json;

namespace DevNexus.Client.Shared.Components.Swarm;

/// <summary>
/// SwarmMonitor - 状态显示和 UI 辅助方法
/// </summary>
public partial class SwarmMonitor
{
    private string GetSwarmPhaseTone()
    {
        if (ContextPackages.Count == 0)
        {
            return "neutral";
        }

        if (ContextPackages.Any(package => string.Equals(package.Status, "Failed", StringComparison.OrdinalIgnoreCase)))
        {
            return "danger";
        }

        if (IsPaused)
        {
            return "warning";
        }

        if (ContextPackages.Any(package => IsExecutingStatus(package.Status)))
        {
            return "active";
        }

        if (ContextPackages.All(package => IsTerminalStatus(package.Status)))
        {
            return "success";
        }

        return "info";
    }

    private string GetSwarmPhaseLabel()
    {
        if (ContextPackages.Count == 0)
        {
            return "等待工作包进入 Swarm";
        }

        if (ContextPackages.Any(package => string.Equals(package.Status, "Failed", StringComparison.OrdinalIgnoreCase)))
        {
            return "存在失败工作包";
        }

        if (IsPaused)
        {
            return "Swarm 已暂停";
        }

        if (ContextPackages.Any(package => IsExecutingStatus(package.Status)))
        {
            return "Swarm 正在执行";
        }

        if (ContextPackages.Any(package => IsEvaluatingStatus(package.Status)))
        {
            return "Swarm 正在评估";
        }

        if (ContextPackages.All(package => IsTerminalStatus(package.Status)))
        {
            return "Swarm 已收尾";
        }

        return "Swarm 正在规划";
    }

    private string GetSwarmPhaseDescription()
    {
        if (ContextPackages.Count == 0)
        {
            return "等待系统拆解工作包并生成执行计划。";
        }

        if (IsPaused)
        {
            return "当前执行流已暂停，节点会保留在当前位置，等待继续指令。";
        }

        if (ContextPackages.Any(package => string.Equals(package.Status, "Failed", StringComparison.OrdinalIgnoreCase)))
        {
            return "存在异常工作包，建议优先查看时间轴和工作包详情中的失败原因。";
        }

        if (ContextPackages.Any(package => IsExecutingStatus(package.Status)))
        {
            return "当前正在推进工作包执行链路，相关路径会被自动强调。";
        }

        if (ContextPackages.Any(package => IsEvaluatingStatus(package.Status)))
        {
            return "部分工作包正在评估或重试，属于正常收尾阶段。";
        }

        if (ContextPackages.All(package => IsTerminalStatus(package.Status)))
        {
            return "所有工作包已经结束，当前视图主要用于复盘结果。";
        }

        return "任务已进入编排阶段，正在等待后续执行。";
    }

    private IReadOnlyList<SwarmStageMetric> GetSwarmStageMetrics()
    {
        var planning = ContextPackages.Count(package => string.Equals(package.Status, "Pending", StringComparison.OrdinalIgnoreCase));
        var executing = ContextPackages.Count(package => IsExecutingStatus(package.Status));
        var evaluating = ContextPackages.Count(package => IsEvaluatingStatus(package.Status));
        var closing = ContextPackages.Count(package => IsTerminalStatus(package.Status));

        var metrics = new List<SwarmStageMetric>
        {
            new("规划中", planning, GetSwarmPhaseTone() == "info" || (ContextPackages.Count > 0 && planning > 0 && executing == 0 && evaluating == 0 && closing == 0), "info"),
            new("执行中", executing, GetSwarmPhaseTone() == "active", "active"),
            new("评估中", evaluating, GetSwarmPhaseTone() == "warning" || (ContextPackages.Count > 0 && evaluating > 0 && executing == 0), "warning"),
            new("收尾中", closing, GetSwarmPhaseTone() == "success", "success")
        };

        return metrics;
    }

    private static bool IsTerminalStatus(string status)
    {
        return IsStatus(status, SwarmTaskStatusNames.Completed)
            || IsStatus(status, SwarmTaskStatusNames.Failed)
            || IsStatus(status, SwarmTaskStatusNames.Skipped)
            || IsStatus(status, SwarmTaskStatusNames.Transferred);
    }

    private string GetSelectedPackageStatusClass()
    {
        return SelectedPackage == null ? "status-muted" : GetSwarmTaskStatusClass(SelectedPackage.Status);
    }

    private string GetSelectedPackageStatusText()
    {
        return SelectedPackage == null ? "未选择" : GetSwarmTaskStatusText(SelectedPackage.Status);
    }

    private string GetSelectedPackageGuidance()
    {
        if (SelectedPackage == null)
        {
            return "请选择一个工作包查看上下文边界、依赖关系和执行结果。";
        }

        return SelectedPackage.Status switch
        {
            "Failed" => "该工作包执行失败，建议优先查看结果摘要、依赖链和终端输出，确认下一步处理。",
            "InProgress" => "该工作包正在执行，可结合最近事件和终端输出持续观察。",
            "Evaluating" => "该工作包正在评估或修复，状态可能继续波动。",
            "Completed" => "该工作包已完成，可检查结果是否满足后续依赖。",
            _ => "该工作包尚未结束，可结合依赖链判断当前阻塞点。"
        };
    }

    private string GetSelectedPackageUpdateText()
    {
        if (SelectedPackage == null || SelectedPackage.UpdatedAt == default)
        {
            return "暂无更新时间";
        }

        return $"最近更新：{SelectedPackage.UpdatedAt.ToLocalTime():MM-dd HH:mm:ss}";
    }

    private record SwarmStageMetric(string Label, int Count, bool Active, string Tone);

    private static string GetTimelineTone(string status)
    {
        return status switch
        {
            SwarmTaskStatusNames.Completed => "success",
            SwarmTaskStatusNames.InProgress => "info",
            SwarmTaskStatusNames.Failed => "danger",
            SwarmTaskStatusNames.Evaluating => "warning",
            SwarmTaskStatusNames.Retrying => "warning",
            _ => "neutral"
        };
    }

    private static string GetTaskTimelineStatus(string status)
    {
        return status switch
        {
            SwarmTaskStatusNames.Pending => "等待",
            SwarmTaskStatusNames.InProgress => "执行",
            SwarmTaskStatusNames.Completed => "完成",
            SwarmTaskStatusNames.Failed => "失败",
            SwarmTaskStatusNames.Transferred => "流转",
            SwarmTaskStatusNames.Skipped => "跳过",
            SwarmTaskStatusNames.GroupChatting => "讨论",
            SwarmTaskStatusNames.Evaluating => "评估",
            SwarmTaskStatusNames.Retrying => "重试",
            _ => "更新"
        };
    }

    private static string GetTimelineLabel(string? message, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            return message;
        }

        return fallback;
    }

    private static string GetTaskRuntimeLabel(string status)
    {
        return status switch
        {
            "Pending" => "等待调度",
            "Ready" => "执行准备",
            "InProgress" => "正在执行",
            "Completed" => "已完成",
            "Failed" => "执行失败",
            "Evaluating" => "评估中",
            "Aborted" => "已中止",
            _ => status
        };
    }

    private static string GetSwarmTaskStatusClass(string status)
    {
        return status switch
        {
            "Pending" => "status-pending",
            "Ready" => "status-info",
            "InProgress" => "status-active",
            "Completed" => "status-success",
            "Failed" => "status-danger",
            "Evaluating" => "status-warning",
            "Aborted" => "status-muted",
            _ => "status-neutral"
        };
    }

    private static string GetSwarmTaskStatusText(string status)
    {
        return status switch
        {
            "Pending" => "等待中",
            "Ready" => "已就绪",
            "InProgress" => "执行中",
            "Completed" => "已完成",
            "Failed" => "失败",
            "Evaluating" => "评估中",
            "Aborted" => "已中止",
            _ => status
        };
    }

    private static bool IsStatus(string? status, string expected)
    {
        return string.Equals(status, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExecutingStatus(string? status)
    {
        return IsStatus(status, "InProgress");
    }

    private static bool IsEvaluatingStatus(string? status)
    {
        return IsStatus(status, "Evaluating");
    }

    private static bool IsCancellationReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return false;
        }

        return reason.Contains("cancel", StringComparison.OrdinalIgnoreCase)
               || reason.Contains("取消", StringComparison.OrdinalIgnoreCase)
               || reason.Contains("中止", StringComparison.OrdinalIgnoreCase);
    }
}
