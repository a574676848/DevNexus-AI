using System.Text;
using DevNexus.Core.Abstractions;
using DevNexus.Core.Models.Evaluation;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Agent Loop 修复提示构建器。
/// </summary>
public sealed class AgentRepairPromptBuilder : IRepairContextBuilder
{
    private const int MaxUserGoalLength = 1000;
    private const int MaxPreviousOutputLength = 2000;
    private const string TruncatedMarker = "\n... (已截断)";
    private const string HostWaitCommandToolName = "HostService.WaitCommandAsync";
    private const string HostSendCommandInputToolName = "HostService.SendCommandInputAsync";
    private const string HostStopCommandToolName = "HostService.StopCommandAsync";

    /// <inheritdoc />
    public string Build(EvaluationContext context, EvaluationResult evaluation)
    {
        return PromptFragmentComposer.Compose(
        [
            PromptFragment.RepairInstruction(BuildHeader(context, evaluation), sequence: 0),
            PromptFragment.RepairInstruction(BuildUserGoal(context), sequence: 10),
            PromptFragment.RepairInstruction(BuildFeedback(evaluation), sequence: 20),
            PromptFragment.RepairInstruction(BuildScores(evaluation), sequence: 30),
            PromptFragment.RepairInstruction(BuildSuggestions(evaluation), sequence: 40),
            PromptFragment.RepairInstruction(BuildFailedTools(context), sequence: 50),
            PromptFragment.RepairInstruction(BuildToolRecoveryStrategy(context), sequence: 55),
            PromptFragment.RepairInstruction(BuildPreviousOutput(context), sequence: 60),
            PromptFragment.RepairInstruction(BuildRequirements(), sequence: 70),
            PromptFragment.RepairInstruction(BuildStopPolicy(), sequence: 80)
        ]);
    }

    private static string BuildHeader(EvaluationContext context, EvaluationResult evaluation)
    {
        return $"## 修复指令 (第 {context.Attempt} 次重试)\n\n" +
               $"上一次执行结果未通过质量评估（分数: {evaluation.Score:F1}/100）。";
    }

    private static string BuildUserGoal(EvaluationContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Goal))
        {
            return string.Empty;
        }

        var goal = context.Goal.Length > MaxUserGoalLength
            ? context.Goal[..MaxUserGoalLength] + TruncatedMarker
            : context.Goal;

        return $"### 原始用户目标\n{goal}";
    }

    private static string BuildFeedback(EvaluationResult evaluation)
    {
        return string.IsNullOrWhiteSpace(evaluation.Feedback)
            ? string.Empty
            : $"### 评估反馈\n{evaluation.Feedback}";
    }

    private static string BuildScores(EvaluationResult evaluation)
    {
        return "### 各维度分数\n" +
               $"- 正确性: {evaluation.CorrectnessScore:F0}/100\n" +
               $"- 完整性: {evaluation.CompletenessScore:F0}/100\n" +
               $"- 质量: {evaluation.QualityScore:F0}/100\n" +
               $"- 效率: {evaluation.EfficiencyScore:F0}/100";
    }

    private static string BuildSuggestions(EvaluationResult evaluation)
    {
        if (evaluation.ImprovementSuggestions.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder("### 改进建议");
        for (var index = 0; index < evaluation.ImprovementSuggestions.Count; index++)
        {
            builder.AppendLine();
            builder.Append($"{index + 1}. {evaluation.ImprovementSuggestions[index]}");
        }

        return builder.ToString();
    }

    private static string BuildFailedTools(EvaluationContext context)
    {
        var failedTools = context.ToolRecords?
            .Where(record => !record.Success)
            .ToList();
        if (failedTools == null || failedTools.Count == 0)
        {
            return string.Empty;
        }

        var groupedFailures = failedTools
            .GroupBy(CreateFailureGroupKey)
            .Select(group => new
            {
                Tool = group.First(),
                Count = group.Count()
            })
            .ToList();

        var builder = new StringBuilder("### 失败的工具调用记录");
        foreach (var failure in groupedFailures)
        {
            var tool = failure.Tool;
            builder.AppendLine();
            builder.AppendLine($"- **{tool.ToolName}**");
            if (failure.Count > 1)
            {
                builder.AppendLine($"  occurrences: {failure.Count}");
            }

            builder.AppendLine($"  failureReason: {tool.FailureReason.ToWireValue()}");
            builder.AppendLine($"  retryable: {tool.Retryable}");
            builder.AppendLine($"  requiresHumanIntervention: {tool.RequiresHumanIntervention}");
            builder.AppendLine($"  shouldFallback: {tool.ShouldFallback}");
            builder.AppendLine($"  shouldRotateCredential: {tool.ShouldRotateCredential}");
            builder.AppendLine($"  suggestedAction: {tool.SuggestedAction.ToWireValue()}");
            AppendOptionalToolField(builder, "requestedUserInputKind", tool.RequestedUserInputKind);
            AppendOptionalToolField(builder, "requestedUserInputLabel", tool.RequestedUserInputLabel);
            AppendOptionalToolField(builder, "userMessage", tool.UserMessage);
            builder.Append($"  error: {tool.ErrorSummary ?? "执行失败"}");
        }

        return builder.ToString();
    }

    private static string CreateFailureGroupKey(ToolExecutionRecord record)
    {
        return string.Join(
            "|",
            record.ToolName,
            record.FailureReason.ToWireValue(),
            record.SuggestedAction.ToWireValue(),
            record.ErrorSummary ?? string.Empty,
            record.UserMessage ?? string.Empty);
    }

    private static string BuildToolRecoveryStrategy(EvaluationContext context)
    {
        var toolRecords = context.ToolRecords?.ToList();
        if (toolRecords == null || toolRecords.Count == 0)
        {
            return string.Empty;
        }

        var summary = ToolRecoveryStrategySummaryBuilder.Build(toolRecords);
        if (!summary.HasFailures)
        {
            return string.Empty;
        }

        var builder = new StringBuilder("### 工具恢复策略");
        builder.AppendLine();
        builder.AppendLine($"title: {summary.Title}");
        builder.AppendLine($"primaryAction: {summary.PrimaryAction.ToWireValue()}");
        builder.AppendLine($"orderedActions: {FormatActions(summary.OrderedActions)}");
        builder.AppendLine($"failureReasons: {FormatFailureReasons(summary.FailureReasons)}");
        builder.AppendLine($"message: {summary.Message}");
        builder.Append(BuildRecoveryInstruction(summary.PrimaryAction, HasCliInputRequest(toolRecords)));

        return builder.ToString();
    }

    private static string BuildPreviousOutput(EvaluationContext context)
    {
        var previousOutput = context.Result.Length > MaxPreviousOutputLength
            ? context.Result[..MaxPreviousOutputLength] + TruncatedMarker
            : context.Result;

        return $"### 你之前的输出（摘要）\n```\n{previousOutput}\n```";
    }

    private static string BuildRequirements()
    {
        return "### 要求\n" +
               "请基于以上反馈重新执行任务。确保：\n" +
               "1. 针对评估中被扣分的问题进行针对性修复。\n" +
               "2. 不要简单重复上一次的输出。\n" +
               "3. 如果工具调用失败，分析错误根因并尝试不同方案。\n" +
               "4. 输出完整的修复后结果。";
    }

    private static string BuildStopPolicy()
    {
        return "### 自主决策\n" +
               "如果你认为问题无法通过重试解决（例如：缺少必要权限、环境根本不支持、用户需求本身不合理），\n" +
               "请在回复的最后一行添加标记：`[AGENT_LOOP_STOP]`。\n" +
               "这将停止自动重试，并将当前结果返回给用户。";
    }

    private static void AppendOptionalToolField(StringBuilder builder, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            builder.AppendLine($"  {name}: {value}");
        }
    }

    private static string FormatActions(IReadOnlyList<ToolSuggestedAction> actions)
    {
        return actions.Count == 0
            ? ToolSuggestedAction.None.ToWireValue()
            : string.Join(", ", actions.Select(action => action.ToWireValue()));
    }

    private static string FormatFailureReasons(IReadOnlyList<ToolFailureReason> reasons)
    {
        return reasons.Count == 0
            ? ToolFailureReason.None.ToWireValue()
            : string.Join(", ", reasons.Select(reason => reason.ToWireValue()));
    }

    private static bool HasCliInputRequest(IReadOnlyList<ToolExecutionRecord> toolRecords)
    {
        return toolRecords.Any(CliContinuationRecoveryPolicy.IsInputContinuation);
    }

    private static string BuildRecoveryInstruction(ToolSuggestedAction primaryAction, bool hasCliInputRequest)
    {
        if (hasCliInputRequest)
        {
            return $"执行要求: 当前终端命令正在等待 stdin，必须调用 {HostSendCommandInputToolName} 向同一终端会话发送输入；可先调用 {HostWaitCommandToolName} 查看最新输出。不要重新启动相同命令，也不要把它升级为人工挂起交互。";
        }

        return primaryAction switch
        {
            ToolSuggestedAction.RequestApproval or ToolSuggestedAction.PromptUserInput or ToolSuggestedAction.RefreshCredential =>
                "执行要求: 当前首要动作存在人工前置条件，不要把它当作普通重试；应先等待审批、补充输入或刷新凭证后再继续。",
            ToolSuggestedAction.WaitForCompletion =>
                $"执行要求: 当前终端命令仍在运行，必须优先调用 {HostWaitCommandToolName} 续接同一终端会话；如果状态要求输入，再调用 {HostSendCommandInputToolName} 发送 stdin。不要重新启动相同命令。",
            ToolSuggestedAction.StopCommand =>
                $"执行要求: 当前终端会话停止请求未完成，必须继续调用 {HostStopCommandToolName} 停止同一终端会话。不要重新启动相同命令，也不要切换到其他工具绕开当前会话。",
            ToolSuggestedAction.Retry =>
                "执行要求: 可以重试，但必须修正参数、范围或上下文，避免原样重复失败调用。",
            ToolSuggestedAction.Fallback =>
                "执行要求: 优先切换备用工具、备用 Provider 或更小范围的执行路径，不要继续依赖同一失败路径。",
            ToolSuggestedAction.Abort =>
                "执行要求: 当前失败不适合自动恢复，应停止本轮重试并向用户说明原因。",
            _ =>
                "执行要求: 先解释失败原因，再选择最小可行恢复路径。"
        };
    }
}
