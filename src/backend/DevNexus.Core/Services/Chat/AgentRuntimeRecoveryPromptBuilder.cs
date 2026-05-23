using DevNexus.Core.Models.Evaluation;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Agent Loop 运行态恢复提示构建器。
/// </summary>
internal static class AgentRuntimeRecoveryPromptBuilder
{
    private const int MaxUserGoalLength = 1000;
    private const int MaxResultLength = 1200;
    private const int MaxFailureSummaryLength = 220;
    private const int MaxStrategyMessageLength = 360;
    private const string HostExecuteCommandToolName = "HostService.ExecuteCommandAsync";
    private const string HostWaitCommandToolName = "HostService.WaitCommandAsync";
    private const string HostSendCommandInputToolName = "HostService.SendCommandInputAsync";
    private const string HostStopCommandToolName = "HostService.StopCommandAsync";
    private const string TruncatedMarker = "\n... (已截断)";

    /// <summary>
    /// 构建不经过通用质量评估的运行态续接提示。
    /// </summary>
    public static string Build(
        string userGoal,
        string result,
        IReadOnlyList<ToolExecutionRecord> toolRecords,
        ToolRecoveryStrategySummary summary,
        ToolSuggestedAction deterministicAction = ToolSuggestedAction.None)
    {
        var effectiveSummary = BuildEffectiveSummary(summary, deterministicAction);

        return PromptFragmentComposer.Compose(
        [
            PromptFragment.RepairInstruction(BuildHeader(effectiveSummary), sequence: 0),
            PromptFragment.RepairInstruction(BuildUserGoal(userGoal), sequence: 10),
            PromptFragment.RepairInstruction(BuildStrategy(effectiveSummary, toolRecords), sequence: 20),
            PromptFragment.RepairInstruction(BuildFailedTools(toolRecords), sequence: 30),
            PromptFragment.RepairInstruction(BuildPreviousOutput(result), sequence: 40),
            PromptFragment.RepairInstruction(BuildRequirements(effectiveSummary.PrimaryAction, toolRecords), sequence: 50)
        ]);
    }

    private static ToolRecoveryStrategySummary BuildEffectiveSummary(
        ToolRecoveryStrategySummary summary,
        ToolSuggestedAction deterministicAction)
    {
        if (deterministicAction == ToolSuggestedAction.None)
        {
            return summary;
        }

        return summary with
        {
            PrimaryAction = deterministicAction,
            Title = ResolveRuntimeTitle(deterministicAction, summary.Title),
            Message = ResolveRuntimeMessage(deterministicAction, summary.Message)
        };
    }

    private static string ResolveRuntimeTitle(ToolSuggestedAction action, string fallback)
    {
        return action switch
        {
            ToolSuggestedAction.StopCommand => "工具恢复需要停止终端命令",
            ToolSuggestedAction.WaitForCompletion => "工具执行仍在运行",
            ToolSuggestedAction.PromptUserInput => "终端 stdin 续接",
            _ => fallback
        };
    }

    private static string ResolveRuntimeMessage(ToolSuggestedAction action, string fallback)
    {
        return action switch
        {
            ToolSuggestedAction.StopCommand => "请停止同一终端会话，不要重新启动相同命令或切换到其他工具。",
            ToolSuggestedAction.WaitForCompletion => "请等待同一终端会话完成或查看最新输出，不要重复启动相同命令。",
            ToolSuggestedAction.PromptUserInput => "当前终端命令正在等待 stdin，应向同一终端会话发送输入。",
            _ => fallback
        };
    }

    private static string BuildHeader(ToolRecoveryStrategySummary summary)
    {
        return $"## 运行态续接指令\n\n当前轮次需要按工具运行态继续处理，直接续接同一执行上下文。\n首要动作: {summary.PrimaryAction.ToWireValue()}";
    }

    private static string BuildUserGoal(string userGoal)
    {
        if (string.IsNullOrWhiteSpace(userGoal))
        {
            return string.Empty;
        }

        return $"### 原始用户目标\n{Truncate(userGoal, MaxUserGoalLength)}";
    }

    private static string BuildStrategy(
        ToolRecoveryStrategySummary summary,
        IReadOnlyList<ToolExecutionRecord> toolRecords)
    {
        if (summary.PrimaryAction == ToolSuggestedAction.PromptUserInput && HasCliInputContinuation(toolRecords))
        {
            return "### 工具恢复策略\n" +
                   "title: 终端 stdin 续接\n" +
                   $"primaryAction: {summary.PrimaryAction.ToWireValue()}\n" +
                   $"orderedActions: {FormatActions(summary.OrderedActions)}\n" +
                   "message: 当前终端命令正在等待 stdin，应向同一终端会话发送输入。";
        }

        return "### 工具恢复策略\n" +
               $"title: {summary.Title}\n" +
               $"primaryAction: {summary.PrimaryAction.ToWireValue()}\n" +
               $"orderedActions: {FormatActions(summary.OrderedActions)}\n" +
               $"message: {CompressStrategyMessage(summary.Message)}";
    }

    private static string BuildFailedTools(IReadOnlyList<ToolExecutionRecord> toolRecords)
    {
        var groupedFailures = toolRecords
            .Where(record => !record.Success)
            .GroupBy(CreateFailureGroupKey)
            .Select(group => new
            {
                Tool = group.First(),
                Count = group.Count()
            })
            .ToList();
        if (groupedFailures.Count == 0)
        {
            return string.Empty;
        }

        return "### 最近失败工具\n" + string.Join(
            "\n",
            groupedFailures.Select(item => FormatFailure(item.Tool, item.Count)));
    }

    private static string CreateFailureGroupKey(ToolExecutionRecord record)
    {
        return string.Join(
            "|",
            record.ToolName,
            record.FailureReason.ToWireValue(),
            record.SuggestedAction.ToWireValue(),
            ResolveFailureSummary(record));
    }

    private static string FormatFailure(ToolExecutionRecord record, int count)
    {
        var occurrences = count > 1
            ? $" | occurrences: {count}"
            : string.Empty;
        var summary = ToolOutputBudgetCompressor.Compress(
            ResolveFailureSummary(record),
            MaxFailureSummaryLength);

        return $"- {record.ToolName}: failureReason={record.FailureReason.ToWireValue()}, " +
               $"suggestedAction={record.SuggestedAction.ToWireValue()} | {summary}{occurrences}";
    }

    private static string ResolveFailureSummary(ToolExecutionRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.ErrorSummary))
        {
            return record.ErrorSummary!;
        }

        if (!string.IsNullOrWhiteSpace(record.UserMessage))
        {
            return record.UserMessage!;
        }

        if (!string.IsNullOrWhiteSpace(record.ErrorMessage))
        {
            return record.ErrorMessage!;
        }

        if (!string.IsNullOrWhiteSpace(record.Output))
        {
            return record.Output!;
        }

        return "无错误摘要";
    }

    private static string BuildPreviousOutput(string result)
    {
        if (string.IsNullOrWhiteSpace(result))
        {
            return string.Empty;
        }

        return $"### 上一轮输出摘要\n```\n{ToolOutputBudgetCompressor.Compress(result, MaxResultLength)}\n```";
    }

    private static string BuildRequirements(
        ToolSuggestedAction primaryAction,
        IReadOnlyList<ToolExecutionRecord> toolRecords)
    {
        if (primaryAction == ToolSuggestedAction.PromptUserInput && HasCliInputContinuation(toolRecords))
        {
            return $"### 执行要求\n必须调用 {HostSendCommandInputToolName} 向同一终端会话发送 stdin；必要时先调用 {HostWaitCommandToolName} 查看最新输出。不要调用 {HostExecuteCommandToolName} 重新启动相同命令，也不要升级为人工挂起交互。";
        }

        return primaryAction switch
        {
            ToolSuggestedAction.WaitForCompletion =>
                $"### 执行要求\n必须优先调用 {HostWaitCommandToolName} 续接同一终端会话；如果状态要求输入，再调用 {HostSendCommandInputToolName}。不要调用 {HostExecuteCommandToolName} 重新启动相同命令。",
            ToolSuggestedAction.StopCommand =>
                $"### 执行要求\n必须继续调用 {HostStopCommandToolName} 停止同一终端会话。不要调用 {HostExecuteCommandToolName} 重新启动相同命令，也不要切换到其他工具绕开当前会话。",
            _ =>
                "### 执行要求\n按工具恢复策略选择最小可行续接动作，不要原样重复失败调用。"
        };
    }

    private static string FormatActions(IReadOnlyList<ToolSuggestedAction> actions)
    {
        return actions.Count == 0
            ? ToolSuggestedAction.None.ToWireValue()
            : string.Join(", ", actions.Select(action => action.ToWireValue()));
    }

    private static bool HasCliInputContinuation(IReadOnlyList<ToolExecutionRecord> toolRecords)
    {
        return toolRecords.Any(CliContinuationRecoveryPolicy.IsInputContinuation);
    }

    private static string CompressStrategyMessage(string message)
    {
        return ToolOutputBudgetCompressor.Compress(message, MaxStrategyMessageLength);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length > maxLength
            ? value[..maxLength] + TruncatedMarker
            : value;
    }
}
