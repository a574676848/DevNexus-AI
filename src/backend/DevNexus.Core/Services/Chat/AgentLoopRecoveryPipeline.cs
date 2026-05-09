namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Agent Loop 恢复管线。
/// </summary>
internal sealed class AgentLoopRecoveryPipeline
{
    private readonly IReadOnlyList<IAgentLoopRecoveryMiddleware> _middlewares;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public AgentLoopRecoveryPipeline(IEnumerable<IAgentLoopRecoveryMiddleware> middlewares)
    {
        _middlewares = middlewares.ToList();
    }

    /// <summary>
    /// 按顺序执行恢复中间件。
    /// </summary>
    public async Task<AgentLoopRecoveryGuardResult?> ExecuteAsync(
        AgentLoopRecoveryContext context,
        CancellationToken cancellationToken)
    {
        foreach (var middleware in _middlewares)
        {
            var result = await middleware.TryHandleAsync(context, cancellationToken);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
