namespace DevNexus.Core.Abstractions;

/// <summary>
/// LLM 评估器标记接口，用于与规则评估器做精确依赖注入区分。
/// </summary>
public interface ILlmResponseEvaluator : IResponseEvaluator
{
}
