using DevNexus.Core.Models.Evaluation;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// 统一响应评估接口，同时服务上下文工作包执行链路与单 Agent 对话
/// 遵循洋葱架构：接口位于 Core 层
/// </summary>
public interface IResponseEvaluator
{
    /// <summary>
    /// 评估 LLM 响应质量
    /// </summary>
    /// <param name="context">评估上下文（通用）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>评估结果</returns>
    Task<EvaluationResult> EvaluateAsync(
        EvaluationContext context,
        CancellationToken cancellationToken = default);
}
