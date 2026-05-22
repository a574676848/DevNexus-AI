using DevNexus.Core.Models.Evaluation;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Agent Loop 完成判定策略。
/// </summary>
internal static class AgentLoopCompletionPolicy
{
    /// <summary>
    /// 判断当前轮次是否可以按普通消息完成。
    /// </summary>
    public static AgentLoopCompletionDecision Decide(IReadOnlyCollection<ToolExecutionRecord> toolRecords)
    {
        return toolRecords.Count == 0
            ? AgentLoopCompletionDecision.Complete("tool_calls_empty")
            : AgentLoopCompletionDecision.EvaluateToolCalls("tool_calls_present");
    }
}

/// <summary>
/// Agent Loop 完成判定结果。
/// </summary>
internal sealed record AgentLoopCompletionDecision
{
    /// <summary>
    /// 是否可以直接完成。
    /// </summary>
    public bool IsComplete { get; init; }

    /// <summary>
    /// 判定原因。
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// 生成完成结果。
    /// </summary>
    public static AgentLoopCompletionDecision Complete(string reason)
    {
        return new AgentLoopCompletionDecision
        {
            IsComplete = true,
            Reason = reason
        };
    }

    /// <summary>
    /// 生成需要继续处理工具调用的结果。
    /// </summary>
    public static AgentLoopCompletionDecision EvaluateToolCalls(string reason)
    {
        return new AgentLoopCompletionDecision
        {
            IsComplete = false,
            Reason = reason
        };
    }
}
