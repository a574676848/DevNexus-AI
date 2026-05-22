namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Agent 单轮任务编排下一步。
/// </summary>
public static class AgentTaskOrchestrationSteps
{
    /// <summary>
    /// 本轮已完成。
    /// </summary>
    public const string Complete = "complete";

    /// <summary>
    /// 继续 Agent Loop 修复。
    /// </summary>
    public const string RetryAgentLoop = "retry-agent-loop";

    /// <summary>
    /// 等待用户处理前置条件。
    /// </summary>
    public const string WaitForUser = "wait-for-user";

    /// <summary>
    /// 处理工具恢复动作。
    /// </summary>
    public const string HandleToolRecovery = "handle-tool-recovery";

    /// <summary>
    /// 执行或等待记忆沉淀。
    /// </summary>
    public const string ConsolidateMemory = "consolidate-memory";
}
