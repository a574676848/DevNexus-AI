using DevNexus.Core.Models.Evaluation;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 上下文溢出恢复提示构建器。
/// </summary>
internal static class ContextOverflowRepairPromptBuilder
{
    private const int MaxUserGoalLength = 1000;
    private const int MaxFailureSummaryLength = 800;
    private const int MaxFailureMessageLength = 220;
    private const int MaxFailureItems = 2;

    /// <summary>
    /// 尝试构建上下文溢出的确定性修复提示。
    /// </summary>
    public static string? TryBuild(string userGoal, IReadOnlyList<ToolExecutionRecord> toolRecords)
    {
        var overflowRecords = toolRecords
            .Where(record => !record.Success && record.FailureReason == ToolFailureReason.ContextOverflow)
            .ToList();
        var timeoutRecords = toolRecords
            .Where(IsLikelyLlmReadTimeout)
            .ToList();
        if (overflowRecords.Count == 0 && timeoutRecords.Count == 0)
        {
            return null;
        }

        var isTimeoutRecovery = overflowRecords.Count == 0;
        var failedRecords = isTimeoutRecovery ? timeoutRecords : overflowRecords;
        var failureSummary = BuildFailureSummary(
            failedRecords,
            isTimeoutRecovery
                ? "上一次 LLM 请求疑似因输出过大或读取超时未完成。"
                : "上一次执行触发上下文或输出长度限制。");
        var title = isTimeoutRecovery ? "## LLM 超时恢复指令" : "## 上下文溢出恢复指令";
        var reason = isTimeoutRecovery
            ? "上一次 LLM 响应疑似因单次输出过大或读取超时未完成，需要拆成更小步骤继续任务。"
            : "上一次执行触发了上下文或输出长度限制，需要改用更轻量的执行路径继续任务。";

        return PromptFragmentComposer.Compose(
        [
            PromptFragment.RepairInstruction(
                title + "\n\n" + reason,
                sequence: 0),
            PromptFragment.RepairInstruction($"### 原始目标\n{CompressPromptText(userGoal, MaxUserGoalLength)}", sequence: 10),
            PromptFragment.RepairInstruction($"### 失败摘要\n{CompressPromptText(failureSummary, MaxFailureSummaryLength)}", sequence: 20),
            PromptFragment.RepairInstruction(
                "### 恢复要求\n" +
                "1. 不要重复提交完整历史、完整大文件或完整工具输出。\n" +
                "2. 先压缩已知上下文，只保留任务目标、关键约束、失败原因和下一步所需证据。\n" +
                "3. 将原任务拆成可验证的小步，每步只产出当前必需的结果。\n" +
                "4. 工具输出必须分批读取或摘要化处理，避免一次性请求超长内容。\n" +
                "5. 如需重新调用工具，优先缩小查询范围、分页读取或读取局部片段。\n" +
                "6. 不要原样重复上一次超时请求，也不要一次性要求模型生成大体量全文。\n" +
                "7. 如果仍然无法在上下文限制内完成，请明确说明需要用户选择更小范围。",
                sequence: 30)
        ]);
    }

    private static string CompressPromptText(string value, int maxLength)
    {
        return ToolOutputBudgetCompressor.Compress(value, maxLength);
    }

    private static string BuildFailureSummary(IReadOnlyList<ToolExecutionRecord> records, string fallback)
    {
        var summaries = records
            .Where(record => !record.Success)
            .Select(FormatFailure)
            .Where(summary => !string.IsNullOrWhiteSpace(summary))
            .Distinct(StringComparer.Ordinal)
            .Take(MaxFailureItems)
            .ToList();

        return summaries.Count == 0
            ? fallback
            : string.Join("\n", summaries);
    }

    private static string FormatFailure(ToolExecutionRecord record)
    {
        var message = ResolveFailureMessage(record);
        var compressedMessage = CompressPromptText(message, MaxFailureMessageLength);

        return "- " + ResolveToolName(record) +
               $": failureReason={record.FailureReason.ToWireValue()}" +
               $", suggestedAction={record.SuggestedAction.ToWireValue()}" +
               $", message={compressedMessage}";
    }

    private static string ResolveToolName(ToolExecutionRecord record)
    {
        return string.IsNullOrWhiteSpace(record.ToolName)
            ? "UnknownTool"
            : record.ToolName;
    }

    private static string ResolveFailureMessage(ToolExecutionRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.UserMessage))
        {
            return record.UserMessage!;
        }

        if (!string.IsNullOrWhiteSpace(record.ErrorSummary))
        {
            return record.ErrorSummary!;
        }

        if (!string.IsNullOrWhiteSpace(record.ErrorMessage))
        {
            return record.ErrorMessage!;
        }

        if (!string.IsNullOrWhiteSpace(record.Output))
        {
            return record.Output!;
        }

        return "工具执行失败，但没有返回可用的错误摘要。";
    }

    private static bool IsLikelyLlmReadTimeout(ToolExecutionRecord record)
    {
        if (record.Success || record.FailureReason != ToolFailureReason.TransientNetworkFailure)
        {
            return false;
        }

        var diagnosticText = string.Join(
            "\n",
            record.UserMessage,
            record.ErrorSummary,
            record.ErrorMessage,
            record.Output);

        return diagnosticText.Contains("read timeout", StringComparison.OrdinalIgnoreCase)
               || diagnosticText.Contains("request timed out", StringComparison.OrdinalIgnoreCase)
               || diagnosticText.Contains("响应超时", StringComparison.OrdinalIgnoreCase)
               || diagnosticText.Contains("读取超时", StringComparison.OrdinalIgnoreCase);
    }
}
