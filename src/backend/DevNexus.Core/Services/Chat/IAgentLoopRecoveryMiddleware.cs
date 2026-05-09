namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Agent Loop 恢复中间件。
/// </summary>
internal interface IAgentLoopRecoveryMiddleware
{
    /// <summary>
    /// 尝试处理当前恢复上下文。
    /// </summary>
    Task<AgentLoopRecoveryGuardResult?> TryHandleAsync(
        AgentLoopRecoveryContext context,
        CancellationToken cancellationToken);
}
