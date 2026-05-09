using DevNexus.Domain.Models.Swarm;

namespace DevNexus.Core.Services.Swarm.Execution;

/// <summary>
/// 上下文工作包执行器接口。
/// </summary>
public interface IContextWorkPackageExecutor
{
    /// <summary>
    /// 执行单个上下文工作包。
    /// </summary>
    Task<SwarmTaskExecutionResult> ExecuteAsync(
        ContextWorkPackage package,
        Guid providerId,
        Guid userId,
        CancellationToken cancellationToken = default,
        string? extraInstruction = null);
}
