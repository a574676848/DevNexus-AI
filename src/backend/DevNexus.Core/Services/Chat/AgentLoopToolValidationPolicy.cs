using DevNexus.Core.Models.Evaluation;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Agent Loop 工具调用验证策略。
/// </summary>
internal static class AgentLoopToolValidationPolicy
{
    /// <summary>
    /// 评估工具记录是否允许进入后续质量评估。
    /// </summary>
    public static AgentLoopToolValidationDecision Decide(
        string userGoal,
        IReadOnlyList<ToolExecutionRecord> toolRecords)
    {
        var validation = ToolExecutionSequenceValidator.Validate(toolRecords);
        if (validation.IsValid)
        {
            return AgentLoopToolValidationDecision.Continue();
        }

        if (ToolCallTruncationRepairPromptBuilder.IsTruncation(validation.Message))
        {
            return AgentLoopToolValidationDecision.Retry(
                ToolCallTruncationRepairPromptBuilder.Build(toolRecords));
        }

        return AgentLoopToolValidationDecision.Stop(
            validation.Message ?? "工具调用协议异常，已停止自动修复。");
    }
}

/// <summary>
/// Agent Loop 工具调用验证裁决。
/// </summary>
internal sealed record AgentLoopToolValidationDecision
{
    /// <summary>
    /// 是否允许继续质量评估。
    /// </summary>
    public bool CanContinue { get; init; }

    /// <summary>
    /// 是否需要直接重试。
    /// </summary>
    public bool NeedsRetry { get; init; }

    /// <summary>
    /// 修复提示。
    /// </summary>
    public string? RepairPrompt { get; init; }

    /// <summary>
    /// 停止说明。
    /// </summary>
    public string? StopMessage { get; init; }

    public static AgentLoopToolValidationDecision Continue()
    {
        return new AgentLoopToolValidationDecision { CanContinue = true };
    }

    public static AgentLoopToolValidationDecision Retry(string repairPrompt)
    {
        return new AgentLoopToolValidationDecision
        {
            NeedsRetry = true,
            RepairPrompt = repairPrompt
        };
    }

    public static AgentLoopToolValidationDecision Stop(string stopMessage)
    {
        return new AgentLoopToolValidationDecision { StopMessage = stopMessage };
    }
}
