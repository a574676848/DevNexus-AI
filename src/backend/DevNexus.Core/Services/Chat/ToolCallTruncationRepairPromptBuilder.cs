using DevNexus.Core.Models.Evaluation;
using DevNexus.Shared.Constants;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 工具调用截断恢复提示构建器。
/// </summary>
internal static class ToolCallTruncationRepairPromptBuilder
{
    /// <summary>
    /// 判断序列验证失败是否属于工具调用截断。
    /// </summary>
    public static bool IsTruncation(string? validationMessage)
    {
        return string.Equals(
            validationMessage,
            AiOptimizationConstants.ToolValidationMessages.TruncatedArguments,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// 构建面向用户和模型的截断恢复指令。
    /// </summary>
    public static string Build(IReadOnlyList<ToolExecutionRecord> toolRecords)
    {
        var toolNames = toolRecords
            .Where(record => !record.Success)
            .Select(record => record.ToolName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToList();
        var toolList = toolNames.Count == 0 ? "未知工具" : string.Join(", ", toolNames);

        return "检测到上游响应可能在工具调用参数生成过程中被截断，已停止本轮自动修复。\n" +
               $"受影响工具: {toolList}\n" +
               "恢复要求:\n" +
               "1. 不要原样重试同一个大工具调用。\n" +
               "2. 将任务拆成更小步骤，优先缩小文件范围、查询范围或命令输出规模。\n" +
               "3. 如果需要写入大段内容，先生成计划或分块补丁，再逐步执行。\n" +
               "4. 重新调用工具时必须提供完整 JSON 参数。";
    }
}
