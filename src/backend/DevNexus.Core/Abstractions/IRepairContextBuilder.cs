using DevNexus.Core.Models.Evaluation;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// 统一修复上下文构建接口
/// 遵循洋葱架构：接口位于 Core 层
/// </summary>
public interface IRepairContextBuilder
{
    /// <summary>
    /// 构建修复上下文（注入到下一轮的 ChatHistory/Agent 指令中）
    /// </summary>
    /// <param name="context">评估上下文</param>
    /// <param name="evaluation">评估结果</param>
    /// <returns>修复指令文本</returns>
    string Build(EvaluationContext context, EvaluationResult evaluation);
}
