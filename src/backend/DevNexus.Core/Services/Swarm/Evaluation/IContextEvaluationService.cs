using DevNexus.Domain.Models.Swarm;

namespace DevNexus.Core.Services.Swarm.Evaluation;

/// <summary>
/// 上下文工作包评估服务接口。
/// </summary>
public interface IContextEvaluationService
{
    /// <summary>
    /// 对执行计划中的工作包结果进行评估。
    /// </summary>
    Task EvaluateAsync(
        SwarmExecutionPlan plan,
        CancellationToken cancellationToken = default);
}
