using DevNexus.Core.Models.Evaluation;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 工具恢复策略摘要。
/// </summary>
internal sealed record ToolRecoveryStrategySummary
{
    /// <summary>
    /// 是否存在失败工具。
    /// </summary>
    public bool HasFailures { get; init; }

    /// <summary>
    /// 首要恢复动作。
    /// </summary>
    public ToolSuggestedAction PrimaryAction { get; init; } = ToolSuggestedAction.None;

    /// <summary>
    /// 按优先级稳定排序后的恢复动作。
    /// </summary>
    public IReadOnlyList<ToolSuggestedAction> OrderedActions { get; init; } = Array.Empty<ToolSuggestedAction>();

    /// <summary>
    /// 失败原因集合。
    /// </summary>
    public IReadOnlyList<ToolFailureReason> FailureReasons { get; init; } = Array.Empty<ToolFailureReason>();

    /// <summary>
    /// 策略标题。
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 策略说明。
    /// </summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// 工具恢复策略摘要构建器。
/// </summary>
internal static class ToolRecoveryStrategySummaryBuilder
{
    /// <summary>
    /// 构建同轮工具失败的恢复策略摘要。
    /// </summary>
    public static ToolRecoveryStrategySummary Build(IReadOnlyList<ToolExecutionRecord> toolRecords)
    {
        var failures = toolRecords
            .Where(record => !record.Success)
            .ToList();
        if (failures.Count == 0)
        {
            return new ToolRecoveryStrategySummary
            {
                Title = "工具执行完成",
                Message = "本轮工具执行未发现失败。"
            };
        }

        var orderedActions = ResolveOrderedActions(failures);
        var primaryAction = orderedActions.FirstOrDefault();
        var digest = ToolExecutionEventSummaryBuilder.BuildFailureDigest(failures);

        return new ToolRecoveryStrategySummary
        {
            HasFailures = true,
            PrimaryAction = primaryAction,
            OrderedActions = orderedActions,
            FailureReasons = failures
                .Select(record => record.FailureReason)
                .Where(reason => reason != ToolFailureReason.None)
                .Distinct()
                .ToList(),
            Title = ResolveTitle(primaryAction),
            Message = ResolveMessage(primaryAction, digest)
        };
    }

    /// <summary>
    /// 按恢复策略选择最优先需要挂起交互的工具。
    /// </summary>
    public static ToolExecutionRecord? SelectPendingInteractionTool(
        IReadOnlyList<ToolExecutionRecord> toolRecords,
        ToolRecoveryStrategySummary summary)
    {
        var candidates = toolRecords
            .Where(record => !record.Success && record.RequiresHumanIntervention)
            .ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        return summary.OrderedActions
                   .Select(action => candidates.FirstOrDefault(record => record.SuggestedAction == action))
                   .FirstOrDefault(record => record != null)
               ?? candidates[0];
    }

    private static IReadOnlyList<ToolSuggestedAction> ResolveOrderedActions(IReadOnlyList<ToolExecutionRecord> failures)
    {
        var actions = failures
            .Select(ResolveAction)
            .Where(action => action != ToolSuggestedAction.None)
            .Distinct()
            .ToHashSet();

        return ToolSuggestedActionExtensions.GetRecoveryPriority()
            .Where(actions.Contains)
            .ToList();
    }

    private static ToolSuggestedAction ResolveAction(ToolExecutionRecord record)
    {
        if (record.SuggestedAction != ToolSuggestedAction.None)
        {
            return record.SuggestedAction;
        }

        if (record.ShouldRotateCredential)
        {
            return ToolSuggestedAction.RefreshCredential;
        }

        if (record.Retryable)
        {
            return ToolSuggestedAction.Retry;
        }

        if (record.ShouldFallback)
        {
            return ToolSuggestedAction.Fallback;
        }

        return record.FailureReason == ToolFailureReason.FatalExecutionError
            ? ToolSuggestedAction.Abort
            : ToolSuggestedAction.Fallback;
    }

    private static string ResolveTitle(ToolSuggestedAction primaryAction)
    {
        return primaryAction switch
        {
            ToolSuggestedAction.RequestApproval => "工具恢复需要审批",
            ToolSuggestedAction.PromptUserInput => "工具恢复需要补充输入",
            ToolSuggestedAction.RefreshCredential => "工具恢复需要刷新凭证",
            ToolSuggestedAction.StopCommand => "工具恢复需要停止终端命令",
            ToolSuggestedAction.WaitForCompletion => "工具执行仍在运行",
            ToolSuggestedAction.Retry => "工具恢复建议重试",
            ToolSuggestedAction.Fallback => "工具恢复建议降级",
            ToolSuggestedAction.Abort => "工具恢复建议终止",
            _ => "工具恢复策略待确认"
        };
    }

    private static string ResolveMessage(ToolSuggestedAction primaryAction, string digest)
    {
        var prefix = primaryAction switch
        {
            ToolSuggestedAction.RequestApproval => "请先完成审批，再继续自动执行。",
            ToolSuggestedAction.PromptUserInput => "请先补充必要输入，再继续自动执行。",
            ToolSuggestedAction.RefreshCredential => "请先刷新或补充凭证，再继续自动执行。",
            ToolSuggestedAction.StopCommand => "请停止同一终端会话，不要重新启动相同命令或切换到其他工具。",
            ToolSuggestedAction.WaitForCompletion => "请等待同一终端会话完成或查看最新输出，不要重复启动相同命令。",
            ToolSuggestedAction.Retry => "可按当前上下文重试，但应避免重复提交无效参数。",
            ToolSuggestedAction.Fallback => "应切换备用工具、备用 Provider 或更小范围的执行路径。",
            ToolSuggestedAction.Abort => "当前失败不适合继续自动恢复，应停止本轮执行。",
            _ => "当前工具失败缺少明确恢复动作。"
        };

        return string.IsNullOrWhiteSpace(digest)
            ? prefix
            : $"{prefix}{digest}";
    }
}
