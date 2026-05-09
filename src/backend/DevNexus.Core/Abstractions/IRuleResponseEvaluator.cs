namespace DevNexus.Core.Abstractions;

/// <summary>
/// 规则评估器标记接口，用于与 LLM 评估器做精确依赖注入区分。
/// </summary>
public interface IRuleResponseEvaluator : IResponseEvaluator
{
}
