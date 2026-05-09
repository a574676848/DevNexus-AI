namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 循环守卫中间件。
/// 负责拦截明显不可恢复的失败轮次。
/// </summary>
internal sealed class LoopGuardMiddleware : IAgentLoopRecoveryMiddleware
{
    /// <inheritdoc />
    public Task<AgentLoopRecoveryGuardResult?> TryHandleAsync(
        AgentLoopRecoveryContext context,
        CancellationToken cancellationToken)
    {
        if (context.AgentLoopAttempt <= 0)
        {
            return Task.FromResult<AgentLoopRecoveryGuardResult?>(null);
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
        var summaries = toolRecords
            .Where(record => !record.Success)
            .Select(record => record.UserMessage ?? record.ErrorSummary ?? record.ErrorMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToList();

        if (summaries.Count == 0)
        {
            return "本轮工具执行均未成功，且失败不具备自动重试条件。请先处理前置条件后再继续。";
        }

        return $"本轮工具执行均未成功，且失败不具备自动重试条件。{string.Join("；", summaries)}";
    }
}
