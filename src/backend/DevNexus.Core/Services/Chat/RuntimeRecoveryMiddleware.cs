namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 运行态恢复中间件。
/// 负责拦截待处理交互与显式的人类干预请求。
/// </summary>
internal sealed class RuntimeRecoveryMiddleware : IAgentLoopRecoveryMiddleware
{
    /// <inheritdoc />
    public Task<AgentLoopRecoveryGuardResult?> TryHandleAsync(
        AgentLoopRecoveryContext context,
        CancellationToken cancellationToken)
    {
        var strategySummary = ToolRecoveryStrategySummaryBuilder.Build(context.ToolRecords);

        if (context.Runtime.PendingInteractionCount > 0)
        {
            var summary = context.Runtime.PrimaryPendingInteractionSummary;
            return Task.FromResult<AgentLoopRecoveryGuardResult?>(new AgentLoopRecoveryGuardResult
            {
                ToolRecords = context.ToolRecords,
                ShouldStop = true,
                StopTitle = !string.IsNullOrWhiteSpace(summary?.Label)
                    ? summary.Label
                    : string.IsNullOrWhiteSpace(context.Runtime.PrimaryPendingInteractionTitle)
                    ? "等待用户处理"
                    : context.Runtime.PrimaryPendingInteractionTitle,
                StopMessage = !string.IsNullOrWhiteSpace(summary?.Description)
                    ? summary.Description
                    : string.IsNullOrWhiteSpace(context.Runtime.PrimaryPendingInteractionDescription)
                    ? "当前会话仍有待处理交互，自动修复已停止。请先完成审批或补充信息。"
                    : context.Runtime.PrimaryPendingInteractionDescription
            });
        }

        var interactionTool = ToolRecoveryStrategySummaryBuilder.SelectPendingInteractionTool(
            context.ToolRecords,
            strategySummary);
        if (interactionTool == null)
        {
            return Task.FromResult<AgentLoopRecoveryGuardResult?>(null);
        }

        return Task.FromResult<AgentLoopRecoveryGuardResult?>(new AgentLoopRecoveryGuardResult
        {
            ToolRecords = context.ToolRecords,
            RequiresPendingInteraction = true,
            PendingInteractionTool = interactionTool
        });
    }
}
