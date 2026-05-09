using DevNexus.Domain.Models.Swarm;

namespace DevNexus.Core.Services.Swarm.Execution;

/// <summary>
/// 执行策略选择服务接口。
/// </summary>
public interface IExecutionStrategySelector
{
    /// <summary>
    /// 为单个上下文工作包选择执行策略。
    /// </summary>
    Task<ContextWorkPackage> SelectAsync(
        ContextWorkPackage package,
        CancellationToken cancellationToken = default);
}
