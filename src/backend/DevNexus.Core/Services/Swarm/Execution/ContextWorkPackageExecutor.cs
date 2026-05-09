using DevNexus.Core.Services.Swarm.Analysis;
using DevNexus.Core.Services.Swarm.Planning;
using DevNexus.Domain.Models.Swarm;
using Microsoft.Extensions.Logging;

namespace DevNexus.Core.Services.Swarm.Execution;

/// <summary>
/// 上下文工作包执行器。
/// </summary>
public class ContextWorkPackageExecutor : IContextWorkPackageExecutor
{
    private readonly ISwarmTaskExecutor _taskExecutor;
    private readonly ILogger<ContextWorkPackageExecutor> _logger;

    /// <summary>
    /// 初始化工作包执行器。
    /// </summary>
    public ContextWorkPackageExecutor(
        ISwarmTaskExecutor taskExecutor,
        ILogger<ContextWorkPackageExecutor> logger)
    {
        _taskExecutor = taskExecutor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SwarmTaskExecutionResult> ExecuteAsync(
        ContextWorkPackage package,
        Guid providerId,
        Guid userId,
        CancellationToken cancellationToken = default,
        string? extraInstruction = null)
    {
        _logger.LogInformation(
            "开始执行上下文工作包 | SessionId={SessionId} PackageId={PackageId} Strategy={Strategy}",
            package.SessionId,
            package.Id,
            package.ExecutionStrategy);

        return await _taskExecutor.ExecutePackageAsync(
            package,
            providerId,
            userId,
            extraInstruction,
            cancellationToken);
    }
}
