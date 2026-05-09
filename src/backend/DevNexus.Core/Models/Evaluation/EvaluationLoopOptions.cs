namespace DevNexus.Core.Models.Evaluation;

/// <summary>
/// 评估-反馈循环的执行配置
/// 从 Swarm 下移到 Core.Models 供全局使用
/// </summary>
public class EvaluationLoopOptions
{
    public int MaxRetries { get; set; } = 3;
    public int BaseDelayMs { get; set; } = 500;
    public bool Enabled { get; set; } = true;
    public double ComplexityThreshold { get; set; } = 50.0;
    public int TokenBudget { get; set; } = 100000;
}
