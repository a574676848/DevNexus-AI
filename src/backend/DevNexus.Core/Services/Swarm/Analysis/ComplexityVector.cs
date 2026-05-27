namespace DevNexus.Core.Services.Swarm.Analysis;

/// <summary>
/// 任务复杂度向量
/// 包含 7 个维度的评估指标，用于判断是否需要 Swarm 多智能体协作
/// </summary>
public record ComplexityVector
{
    /// <summary>
    /// 主要领域
    /// </summary>
    public DomainType PrimaryDomain { get; init; } = DomainType.General;

    /// <summary>
    /// 语义熵 - 请求的模糊度 (0-1)
    /// 0: 意图完全明确, 1: 极其模糊需要大量澄清
    /// </summary>
    public double SemanticEntropy { get; init; }

    /// <summary>
    /// 技能跨度 - 需要多少不同领域专业知识 (0-10)
    /// 0: 单一领域, 10: 涉及极多领域（前端+后端+数据库+运维+安全...）
    /// </summary>
    public double SkillBreadth { get; init; }

    /// <summary>
    /// 上下文深度 - 需要的历史信息量 (0-10)
    /// 0: 无需历史上下文, 10: 强依赖长对话历史
    /// </summary>
    public double ContextDepth { get; init; }

    /// <summary>
    /// 工具复杂度 - 需要使用的工具类型数量 (0-10)
    /// 0: 无需工具, 10: 需组合复杂工具链
    /// </summary>
    public double ToolComplexity { get; init; }

    /// <summary>
    /// 风险等级 - 操作的破坏性程度 (0-10)
    /// 0: 安全查询, 10: 破坏性操作/数据删除/系统配置修改
    /// </summary>
    public double RiskLevel { get; init; }

    /// <summary>
    /// 任务规模 - 预期交付物的规模和工作量 (0-10)
    /// 0: 单函数/单文件, 5: 多文件模块, 10: 完整全栈应用/大型系统
    /// </summary>
    public double TaskScale { get; init; }

    /// <summary>
    /// 工作流深度 - 完成任务需要的阶段数 (0-10)
    /// 0: 一步到位, 5: 需设计+实现+测试, 10: 需求分析+架构+多模块开发+集成+部署
    /// </summary>
    public double StepComplexity { get; init; }

    /// <summary>
    /// 是否来自复杂度评估失败后的兜底结果。
    /// </summary>
    public bool IsEvaluationFallback { get; init; }

    /// <summary>
    /// 复杂度评估失败原因，仅用于结构化诊断。
    /// </summary>
    public string? EvaluationFailureReason { get; init; }

    /// <summary>
    /// 综合评分 (0-100)
    /// 权重分配：任务规模(25%) + 工作流深度(20%) + 技能跨度(15%) +
    ///           工具复杂度(15%) + 语义熵(10%) + 风险等级(10%) + 上下文深度(5%)
    /// </summary>
    public double CompositeScore =>
        ((TaskScale / 10.0) * 25) +         // 25% - 核心维度：任务规模
        ((StepComplexity / 10.0) * 20) +    // 20% - 核心维度：工作流深度
        ((SkillBreadth / 10.0) * 15) +      // 15% - 跨领域程度
        ((ToolComplexity / 10.0) * 15) +    // 15% - 工具链复杂度
        (SemanticEntropy * 10) +            // 10% - 语义模糊度 (0-1 范围)
        ((RiskLevel / 10.0) * 10) +         // 10% - 风险等级（降权）
        ((ContextDepth / 10.0) * 5);        //  5% - 上下文依赖

    /// <summary>
    /// 获取建议的处理模式
    /// </summary>
    public string SuggestedMode => CompositeScore switch
    {
        < 30 => "QuickResponse",
        < 60 => "LightCollaboration",
        _ => "FullSwarm"
    };
}
