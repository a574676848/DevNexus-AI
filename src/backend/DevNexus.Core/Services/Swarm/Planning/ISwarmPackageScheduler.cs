using DevNexus.Domain.Models.Swarm;

namespace DevNexus.Core.Services.Swarm.Planning;

/// <summary>
/// 上下文工作包调度器接口。
/// </summary>
public interface ISwarmPackageScheduler
{
    /// <summary>
    /// 执行工作包计划。
    /// </summary>
    Task ExecuteAsync(
        SwarmExecutionPlan plan,
        Guid providerId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
