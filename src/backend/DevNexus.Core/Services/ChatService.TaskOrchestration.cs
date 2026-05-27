using DevNexus.Core.Models.Evaluation;
using DevNexus.Core.Services.Chat;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace DevNexus.Core.Services;

/// <summary>
/// 聊天服务 - 任务编排快照。
/// </summary>
public partial class ChatService
{
    private async Task<ChatMessageDto> CompleteSystemExperienceReplayAsync(
        ChatMessage aiMessage,
        ChatSession chatSession,
        Guid userId,
        Core.DTOs.ExperienceMatchDto matchResult,
        SystemExperienceReplayDecision replayDecision,
        ChannelWriter<BlockDto> blockWriter,
        CancellationToken cancellationToken)
    {
        var directContent = matchResult.Experience.SolutionSop;
        var replaySnapshot = SystemExperienceReplaySnapshot.FromDecision(replayDecision);
        await blockWriter.WriteAsync(new BlockDto
        {
            BlockType = BlockType.TextDelta,
            Content = directContent,
            MessageId = aiMessage.Id,
            SessionId = chatSession.Id
        }, cancellationToken);

        await blockWriter.WriteAsync(new BlockDto
        {
            BlockType = BlockType.TextDelta,
            MessageId = aiMessage.Id,
            SessionId = chatSession.Id,
            IsLast = true
        }, cancellationToken);

        aiMessage.Metadata ??= new Dictionary<string, object>();
        SystemExperienceReplayMetadata.ApplyDirectHit(aiMessage.Metadata, replayDecision);
        aiMessage.Content = new Dictionary<string, object> { { ChatMessageContentKeys.Text, directContent } };
        aiMessage.Status = ChatConstants.StatusCompleted;
        aiMessage.UpdatedAt = DateTime.UtcNow;
        await _chatMessageRepository.UpdateAsync(aiMessage, cancellationToken);

        await _agentMemoryService.BoostExperienceAsync(matchResult.Experience.Id, cancellationToken);
        await _chatMessageCompletionCoordinator.HandleCompletedAsync(
            chatSession,
            aiMessage,
            userId,
            agentLoopAttempt: 0,
            directContent.Length,
            includeExperienceDistillation: false,
            selfIterationCandidate: null,
            cancellationToken);

        var memoryDecision = await TriggerMemoryConsolidationCheckAsync(
            chatSession,
            userId,
            ChatHistoryGovernanceSnapshot.Empty,
            cancellationToken);
        var taskSnapshot = LogTaskOrchestrationSnapshot(
            aiMessage.Id,
            agentLoopAttempt: 0,
            AgentLoopAction.None,
            ChatHistoryGovernanceSnapshot.Empty,
            replaySnapshot,
            memoryDecision,
            []);
        var selfIterationCandidate = EvaluateExperienceDistillationCandidate(taskSnapshot);
        SelfIterationCandidateMetadata.Apply(aiMessage.Metadata, selfIterationCandidate);
        await _chatMessageRepository.UpdateAsync(aiMessage, cancellationToken);

        return new ChatMessageDto
        {
            Id = aiMessage.Id,
            ChatSessionId = chatSession.Id,
            SenderId = aiMessage.SenderId,
            SenderType = aiMessage.SenderType,
            Content = directContent,
            MessageType = aiMessage.MessageType,
            CreatedAt = aiMessage.CreatedAt,
            Status = aiMessage.Status,
            Metadata = aiMessage.Metadata
        };
    }

    private AgentTaskOrchestrationSnapshot LogTaskOrchestrationSnapshot(
        Guid turnId,
        int agentLoopAttempt,
        AgentLoopAction agentLoopAction,
        ChatHistoryGovernanceSnapshot? historyGovernance,
        SystemExperienceReplaySnapshot? systemExperienceReplay,
        MemoryConsolidationTriggerDecision? memoryDecision,
        IReadOnlyList<ToolExecutionRecord> toolRecords)
    {
        var snapshot = AgentTaskOrchestrationSnapshotBuilder.Build(
            turnId,
            agentLoopAttempt,
            agentLoopAction,
            historyGovernance,
            systemExperienceReplay,
            memoryDecision,
            toolRecords);

        _logger.LogDebug(
            "[AI.Task.Orchestration] Snapshot built | TurnId={TurnId} Attempt={Attempt} Action={Action} " +
            "ContextStrategy={ContextStrategy} ContextPressure={ContextPressure} ContextPressureReason={ContextPressureReason} " +
            "ContextCompressionIndexed={ContextCompressionIndexed} ContextCompressionTopics={ContextCompressionTopics} " +
            "ContextCompressionSummaryChars={ContextCompressionSummaryChars} " +
            "ContextCompressionSummaryFingerprint={ContextCompressionSummaryFingerprint} " +
            "ExperienceReplay={ExperienceReplay} ExperienceDirect={ExperienceDirect} ExperienceDynamic={ExperienceDynamic} " +
            "ExperienceReplayReason={ExperienceReplayReason} ExperienceId={ExperienceId} Similarity={Similarity} " +
            "ExperienceHasDistillationProtocol={ExperienceHasDistillationProtocol} ExperienceHasSelfIterationFacts={ExperienceHasSelfIterationFacts} " +
            "ExperienceCandidateReason={ExperienceCandidateReason} ExperienceContextPressureReason={ExperienceContextPressureReason} " +
            "ExperienceContextCompressionSummaryFingerprint={ExperienceContextCompressionSummaryFingerprint} " +
            "ExperienceValueSignal={ExperienceValueSignal} " +
            "ExperienceSourceSessionId={ExperienceSourceSessionId} " +
            "ExperienceDistillationPromptFingerprint={ExperienceDistillationPromptFingerprint} " +
            "ExperienceCitationFingerprint={ExperienceCitationFingerprint} " +
            "MemoryReason={MemoryReason} MemoryImmediate={MemoryImmediate} MemoryDelayed={MemoryDelayed} " +
            "ToolEvents={ToolEvents} FailedToolEvents={FailedToolEvents} PrimaryAction={PrimaryAction} NextStep={NextStep}",
            snapshot.TurnId,
            snapshot.AgentLoopAttempt,
            snapshot.AgentLoopAction,
            snapshot.ContextStrategy,
            snapshot.HasContextPressure,
            snapshot.ContextPressureReason,
            snapshot.ContextCompressionIndex.HasIndex,
            snapshot.ContextCompressionIndex.TopicHints.Count,
            snapshot.ContextCompressionIndex.SummaryCharacterCount,
            snapshot.ContextCompressionIndex.SummaryFingerprint,
            snapshot.SystemExperienceReplay.WasReplayed,
            snapshot.SystemExperienceReplay.AnsweredDirectly,
            snapshot.SystemExperienceReplay.InjectedDynamicContext,
            snapshot.SystemExperienceReplay.Reason,
            snapshot.SystemExperienceReplay.ExperienceId,
            snapshot.SystemExperienceReplay.Similarity,
            snapshot.SystemExperienceReplay.ContextTagSnapshot.HasDistillationProtocol,
            snapshot.SystemExperienceReplay.ContextTagSnapshot.HasSelfIterationFacts,
            snapshot.SystemExperienceReplay.ContextTagSnapshot.CandidateReason,
            snapshot.SystemExperienceReplay.ContextTagSnapshot.ContextPressureReason,
            snapshot.SystemExperienceReplay.ContextTagSnapshot.ContextCompressionSummaryFingerprint,
            snapshot.ExperienceValueSignalKeyword,
            snapshot.ExperienceSourceSessionId,
            snapshot.ExperienceDistillationPromptFingerprint,
            snapshot.ExperienceMemoryCitation.CitationFingerprint,
            snapshot.MemoryTriggerReason,
            snapshot.MemoryEnqueuedImmediately,
            snapshot.MemoryScheduledDelayed,
            snapshot.ToolEventCount,
            snapshot.FailedToolEventCount,
            snapshot.PrimarySuggestedAction,
            snapshot.NextStep);

        LogSystemExperienceReplayEvaluation(snapshot);

        return snapshot;
    }

    /// <summary>
    /// 记录系统经验回放效果事实，供治理复盘使用。
    /// </summary>
    /// <param name="snapshot">任务编排快照。</param>
    private void LogSystemExperienceReplayEvaluation(AgentTaskOrchestrationSnapshot snapshot)
    {
        var evaluation = SystemExperienceReplayEvaluation.Build(snapshot.SystemExperienceReplay);
        _logger.LogDebug(
            "[AI.Memory.ReplayEvaluation] Replay evaluated | TurnId={TurnId} ReplayReason={ReplayReason} " +
            "UsefulRecall={UsefulRecall} ContextPollutionRisk={ContextPollutionRisk} " +
            "UntraceableReuseRisk={UntraceableReuseRisk} EvaluationReason={EvaluationReason} " +
            "Similarity={Similarity} HasCitationFingerprint={HasCitationFingerprint} " +
            "HasValueSignal={HasValueSignal} HasSourceSession={HasSourceSession} " +
            "HasDistillationPromptFingerprint={HasDistillationPromptFingerprint}",
            snapshot.TurnId,
            evaluation.ReplayReason,
            evaluation.UsefulRecall,
            evaluation.ContextPollutionRisk,
            evaluation.UntraceableReuseRisk,
            evaluation.EvaluationReason,
            evaluation.Similarity,
            evaluation.HasCitationFingerprint,
            evaluation.HasValueSignal,
            evaluation.HasSourceSession,
            evaluation.HasDistillationPromptFingerprint);
    }

    private SelfIterationCandidateDecision EvaluateExperienceDistillationCandidate(
        AgentTaskOrchestrationSnapshot snapshot)
    {
        var decision = SelfIterationCandidatePolicy.Decide(snapshot);
        _logger.LogDebug(
            "[AI.SelfIteration] Candidate evaluated | TurnId={TurnId} ShouldDistill={ShouldDistill} " +
            "ObserveOnly={ObserveOnly} Reason={Reason} ContextPressureReason={ContextPressureReason} " +
            "ContextCompressionSummaryFingerprint={ContextCompressionSummaryFingerprint} " +
            "ReusedExperienceHasSelfIterationFacts={ReusedExperienceHasSelfIterationFacts} " +
            "ReusedExperienceCandidateReason={ReusedExperienceCandidateReason} " +
            "ReusedExperienceContextPressureReason={ReusedExperienceContextPressureReason} " +
            "ReusedExperienceContextCompressionSummaryFingerprint={ReusedExperienceContextCompressionSummaryFingerprint} " +
            "ReusedExperienceValueSignal={ReusedExperienceValueSignal} " +
            "ReusedExperienceSourceSessionId={ReusedExperienceSourceSessionId} " +
            "ReusedExperienceDistillationPromptFingerprint={ReusedExperienceDistillationPromptFingerprint} " +
            "ReusedExperienceCitationFingerprint={ReusedExperienceCitationFingerprint}",
            snapshot.TurnId,
            decision.ShouldDistillExperience,
            decision.ShouldObserveOnly,
            decision.Reason,
            decision.ContextPressureReason,
            decision.ContextCompressionSummaryFingerprint,
            decision.ReusedExperienceHasSelfIterationFacts,
            decision.ReusedExperienceCandidateReason,
            decision.ReusedExperienceContextPressureReason,
            decision.ReusedExperienceContextCompressionSummaryFingerprint,
            decision.ReusedExperienceValueSignalKeyword,
            decision.ReusedExperienceSourceSessionId,
            decision.ReusedExperienceDistillationPromptFingerprint,
            decision.ReusedExperienceMemoryCitation.CitationFingerprint);

        return decision;
    }
}
