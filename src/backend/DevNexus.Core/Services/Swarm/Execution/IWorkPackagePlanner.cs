using DevNexus.Domain.Models.Swarm;

namespace DevNexus.Core.Services.Swarm.Execution;

/// <summary>
/// 工作包规划服务接口。
/// </summary>
public interface IWorkPackagePlanner
{
    /// <summary>
    /// 将上下文工作包组装为最终执行计划。
    /// </summary>
    Task<SwarmExecutionPlan> PlanAsync(
        string sessionId,
        IReadOnlyList<ContextWorkPackage> packages,
        CancellationToken cancellationToken = default);
}
