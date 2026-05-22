using DevNexus.Core.Models.Evaluation;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Agent 单轮任务编排快照构建器。
/// </summary>
public static class AgentTaskOrchestrationSnapshotBuilder
{
    /// <summary>
    /// 根据上下文治理、工具事件、Agent Loop 和记忆触发决策构建编排快照。
    /// </summary>
    public static AgentTaskOrchestrationSnapshot Build(
        Guid turnId,
        int agentLoopAttempt,
        AgentLoopAction agentLoopAction,
        ChatHistoryGovernanceSnapshot? historyGovernance,
        SystemExperienceReplaySnapshot? systemExperienceReplay,
        MemoryConsolidationTriggerDecision? memoryDecision,
        IReadOnlyList<ToolExecutionRecord> toolRecords,
        int responseLength = 0)
    {
        var turnEvents = AgentTurnEventBuilder.BuildUpdatedDto(turnId, toolRecords);
        var contextGovernance = historyGovernance ?? ChatHistoryGovernanceSnapshot.Empty;
        var contextPressure = ChatHistoryPressurePolicy.Summarize(contextGovernance);
        var contextPressureReason = ResolveContextPressureReason(contextPressure, memoryDecision);

        return new AgentTaskOrchestrationSnapshot
        {
            TurnId = turnId,
            AgentLoopAttempt = agentLoopAttempt,
            AgentLoopAction = agentLoopAction,
            ContextStrategy = contextGovernance.Strategy,
            HasContextPressure = contextPressure.HasPressure,
            ContextPressureReason = contextPressureReason,
            ContextCompressionIndex = contextGovernance.CompressionIndex,
            SystemExperienceReplay = systemExperienceReplay ?? SystemExperienceReplaySnapshot.Empty,
            MemoryTriggerReason = memoryDecision?.Reason ?? MemoryConsolidationTriggerReasons.TooFewMessages,
            MemoryEnqueuedImmediately = memoryDecision?.ShouldEnqueueImmediately ?? false,
            MemoryScheduledDelayed = memoryDecision?.ShouldScheduleDelayed ?? false,
            ToolEventCount = turnEvents.EventCount,
            ResponseLength = Math.Max(0, responseLength),
            FailedToolEventCount = turnEvents.FailedEventCount,
            PrimarySuggestedAction = turnEvents.BatchDiagnostics.PrimarySuggestedAction,
            NextStep = ResolveNextStep(agentLoopAction, memoryDecision, turnEvents.BatchDiagnostics.PrimarySuggestedAction)
        };
    }

    private static string ResolveContextPressureReason(
        ChatHistoryPressureSummary contextPressure,
        MemoryConsolidationTriggerDecision? memoryDecision)
    {
        if (contextPressure.HasPressure)
        {
            return contextPressure.PrimaryReason;
        }

        return memoryDecision?.ContextPressureReason ?? ChatHistoryPressureReasons.None;
    }

    private static string ResolveNextStep(
        AgentLoopAction agentLoopAction,
        MemoryConsolidationTriggerDecision? memoryDecision,
        ToolSuggestedAction primarySuggestedAction)
    {
        if (agentLoopAction == AgentLoopAction.Retry)
        {
            return AgentTaskOrchestrationSteps.RetryAgentLoop;
        }

        if (agentLoopAction == AgentLoopAction.Stop)
        {
            return AgentTaskOrchestrationSteps.WaitForUser;
        }

        if (primarySuggestedAction != ToolSuggestedAction.None)
        {
            return AgentTaskOrchestrationSteps.HandleToolRecovery;
        }

        if (memoryDecision?.ShouldEnqueueImmediately == true
            || memoryDecision?.ShouldScheduleDelayed == true)
        {
            return AgentTaskOrchestrationSteps.ConsolidateMemory;
        }

        return AgentTaskOrchestrationSteps.Complete;
    }
}
