using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;

namespace DevNexus.Core.Services.Swarm.Analysis;

/// <summary>
/// 负责评估用户请求的复杂度和所属领域
/// </summary>
public interface IComplexityEvaluator
{
    /// <summary>
    /// 分析请求并返回复杂度向量
    /// </summary>
    /// <param name="userRequest">当前用户输入</param>
    /// <param name="history">聊天历史上下文 (可选)</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>复杂度向量</returns>
    Task<ComplexityVector> EvaluateAsync(
        string userRequest, 
        Guid providerId,
        Microsoft.SemanticKernel.ChatCompletion.ChatHistory? history = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 判断是否应该升级为 Swarm 模式
    /// </summary>
    bool ShouldEscalateToSwarm(ComplexityVector vector);
}
