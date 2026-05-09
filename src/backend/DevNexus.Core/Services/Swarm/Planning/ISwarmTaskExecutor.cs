using DevNexus.Core.Services.Swarm.Execution;
using DevNexus.Domain.Models.Swarm;

namespace DevNexus.Core.Services.Swarm.Planning;

/// <summary>
/// Swarm 工作包执行器接口。
/// </summary>
public interface ISwarmTaskExecutor
{
    /// <summary>
    /// 执行单个上下文工作包。
    /// </summary>
    Task<SwarmTaskExecutionResult> ExecutePackageAsync(
        ContextWorkPackage package,
        Guid providerId,
        Guid userId,
        string? extraInstruction,
        CancellationToken cancellationToken);
}
