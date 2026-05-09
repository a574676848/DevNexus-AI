namespace DevNexus.Domain.Enums;

/// <summary>
/// 上下文工作包执行策略。
/// </summary>
public enum SwarmExecutionStrategy
{
    /// <summary>
    /// 由单 Agent 顺序闭环处理。
    /// </summary>
    SingleAgentSequential = 0,

    /// <summary>
    /// 多个工作包并行处理。
    /// </summary>
    ParallelPackages = 1,

    /// <summary>
    /// 由 Supervisor 负责路由与汇总。
    /// </summary>
    SupervisorRouted = 2,

    /// <summary>
    /// 通过群聊或多轮讨论完成。
    /// </summary>
    GroupDeliberation = 3,

    /// <summary>
    /// 仅执行黑盒验证与结果回报。
    /// </summary>
    BlackBoxValidation = 4
}
