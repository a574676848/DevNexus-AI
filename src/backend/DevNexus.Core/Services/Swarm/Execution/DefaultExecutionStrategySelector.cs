using DevNexus.Domain.Enums;
using DevNexus.Domain.Models.Swarm;

namespace DevNexus.Core.Services.Swarm.Execution;

/// <summary>
/// 默认执行策略选择器。
/// </summary>
public class DefaultExecutionStrategySelector : IExecutionStrategySelector
{
    /// <summary>
    /// 为工作包分配默认执行策略。
    /// </summary>
    public Task<ContextWorkPackage> SelectAsync(
        ContextWorkPackage package,
        CancellationToken cancellationToken = default)
    {
        package.ExecutionStrategy = SelectStrategy(package);

        return Task.FromResult(package);
    }

    /// <summary>
    /// 根据工作包特征选择执行策略。
    /// </summary>
    private static SwarmExecutionStrategy SelectStrategy(ContextWorkPackage package)
    {
        if (package.RiskLevel >= 8)
        {
            return SwarmExecutionStrategy.SingleAgentSequential;
        }

        if (package.ContextType == SwarmContextType.Evidence)
        {
            return SwarmExecutionStrategy.BlackBoxValidation;
        }

        if (package.ContextType == SwarmContextType.Task && package.Dependencies.Count >= 3)
        {
            return SwarmExecutionStrategy.SupervisorRouted;
        }

        if (package.ContextType == SwarmContextType.ApiContract && package.RiskLevel >= 6)
        {
            return SwarmExecutionStrategy.GroupDeliberation;
        }

        if (package.CanRunInParallel && package.Dependencies.Count <= 1)
        {
            return SwarmExecutionStrategy.ParallelPackages;
        }

        if (package.Dependencies.Count >= 2)
        {
            return SwarmExecutionStrategy.SupervisorRouted;
        }

        return SwarmExecutionStrategy.SingleAgentSequential;
    }
}
