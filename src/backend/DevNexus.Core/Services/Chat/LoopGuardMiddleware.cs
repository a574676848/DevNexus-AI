using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 循环守卫中间件。
/// 负责拦截明显不可恢复的失败轮次。
/// </summary>
internal sealed class LoopGuardMiddleware : IAgentLoopRecoveryMiddleware
{
    private const int StopCommandMaxContinuationAttempts = 2;

    /// <inheritdoc />
    public Task<AgentLoopRecoveryGuardResult?> TryHandleAsync(
        AgentLoopRecoveryContext context,
        CancellationToken cancellationToken)
    {
        if (context.RuntimeOnly)
        {
            return Task.FromResult<AgentLoopRecoveryGuardResult?>(null);
        }

        if (context.AgentLoopAttempt <= 0)
        {
            return Task.FromResult<AgentLoopRecoveryGuardResult?>(null);
        }

        if (ShouldStopRepeatedStopCommand(context))
        {
            return Task.FromResult<AgentLoopRecoveryGuardResult?>(new AgentLoopRecoveryGuardResult
            {
                ToolRecords = context.ToolRecords,
                ShouldStop = true,
                StopTitle = "终端停止未闭环",
                StopMessage = "已多次尝试停止同一终端会话但仍未闭环，自动修复已停止。请检查终端状态后再继续。"
            });
        }

        var shouldStop = context.ToolRecords.All(record =>
            !record.Success
            && !record.Retryable
            && !record.RequiresHumanIntervention);
        if (!shouldStop)
        {
            return Task.FromResult<AgentLoopRecoveryGuardResult?>(null);
        }

        return Task.FromResult<AgentLoopRecoveryGuardResult?>(new AgentLoopRecoveryGuardResult
        {
            ToolRecords = context.ToolRecords,
            ShouldStop = true,
            StopTitle = "自动修复已停止",
            StopMessage = BuildUnrecoverableFailureMessage(context.ToolRecords)
        });
    }

    private static string BuildUnrecoverableFailureMessage(IReadOnlyList<Core.Models.Evaluation.ToolExecutionRecord> toolRecords)
    {
        var summary = ToolRecoveryStrategySummaryBuilder.Build(toolRecords);
        return $"本轮工具执行均未成功，且失败不具备自动重试条件。{summary.Message}";
    }

    private static bool ShouldStopRepeatedStopCommand(AgentLoopRecoveryContext context)
    {
        return context.AgentLoopAttempt >= StopCommandMaxContinuationAttempts
            && context.ToolRecords.Count > 0
            && context.ToolRecords.All(record =>
                !record.Success
                && record.SuggestedAction == ToolSuggestedAction.StopCommand);
    }
}
