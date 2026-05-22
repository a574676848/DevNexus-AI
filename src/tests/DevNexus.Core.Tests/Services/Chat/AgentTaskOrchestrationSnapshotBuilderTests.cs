using DevNexus.Core.Models.Evaluation;
using DevNexus.Core.Services.Chat;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// Agent 单轮任务编排快照构建器测试。
/// </summary>
public sealed class AgentTaskOrchestrationSnapshotBuilderTests
{
    /// <summary>
    /// Agent Loop 重试应成为首要下一步。
    /// </summary>
    [Fact]
    public void Build_ShouldPreferRetry_WhenAgentLoopRequestsRetry()
    {
        var snapshot = AgentTaskOrchestrationSnapshotBuilder.Build(
            Guid.NewGuid(),
            agentLoopAttempt: 1,
            AgentLoopAction.Retry,
            ChatHistoryGovernanceSnapshot.Empty,
            null,
            null,
            [],
            responseLength: 42);

        snapshot.NextStep.Should().Be(AgentTaskOrchestrationSteps.RetryAgentLoop);
        snapshot.AgentLoopAction.Should().Be(AgentLoopAction.Retry);
        snapshot.ResponseLength.Should().Be(42);
    }

    /// <summary>
    /// Agent Loop 停止应转为等待用户。
    /// </summary>
    [Fact]
    public void Build_ShouldWaitForUser_WhenAgentLoopStops()
    {
        var snapshot = AgentTaskOrchestrationSnapshotBuilder.Build(
            Guid.NewGuid(),
            agentLoopAttempt: 0,
            AgentLoopAction.Stop,
            ChatHistoryGovernanceSnapshot.Empty,
            null,
            null,
            []);

        snapshot.NextStep.Should().Be(AgentTaskOrchestrationSteps.WaitForUser);
    }

    /// <summary>
    /// 工具失败恢复应优先于普通完成。
    /// </summary>
    [Fact]
    public void Build_ShouldHandleToolRecovery_WhenToolHasSuggestedAction()
    {
        var snapshot = AgentTaskOrchestrationSnapshotBuilder.Build(
            Guid.NewGuid(),
            agentLoopAttempt: 0,
            AgentLoopAction.None,
            ChatHistoryGovernanceSnapshot.Empty,
            null,
            null,
            [
                CreateRecord(
                    "Approval.Apply",
                    success: false,
                    ToolFailureReason.ApprovalRequired,
                    ToolSuggestedAction.RequestApproval)
            ]);

        snapshot.NextStep.Should().Be(AgentTaskOrchestrationSteps.HandleToolRecovery);
        snapshot.FailedToolEventCount.Should().Be(1);
        snapshot.PrimarySuggestedAction.Should().Be(ToolSuggestedAction.RequestApproval);
    }

    /// <summary>
    /// 无工具恢复时，记忆沉淀决策应成为下一步。
    /// </summary>
    [Fact]
    public void Build_ShouldConsolidateMemory_WhenMemoryDecisionIsScheduled()
    {
        var snapshot = AgentTaskOrchestrationSnapshotBuilder.Build(
            Guid.NewGuid(),
            agentLoopAttempt: 0,
            AgentLoopAction.None,
            ChatHistoryGovernanceSnapshot.Empty,
            null,
            new MemoryConsolidationTriggerDecision
            {
                ShouldScheduleDelayed = true,
                Reason = MemoryConsolidationTriggerReasons.IdleDelayScheduled
            },
            []);

        snapshot.NextStep.Should().Be(AgentTaskOrchestrationSteps.ConsolidateMemory);
        snapshot.MemoryScheduledDelayed.Should().BeTrue();
        snapshot.MemoryTriggerReason.Should().Be(MemoryConsolidationTriggerReasons.IdleDelayScheduled);
    }

    /// <summary>
    /// 上下文摘要压缩应标记上下文压力。
    /// </summary>
    [Fact]
    public void Build_ShouldMarkContextPressure_WhenHistoryWasCompressed()
    {
        var snapshot = AgentTaskOrchestrationSnapshotBuilder.Build(
            Guid.NewGuid(),
            agentLoopAttempt: 0,
            AgentLoopAction.None,
            new ChatHistoryGovernanceSnapshot
            {
                Strategy = ChatHistoryGovernanceStrategies.SummaryWithRecentSlice,
                SummaryMessageCount = 1
            },
            null,
            null,
            []);

        snapshot.HasContextPressure.Should().BeTrue();
        snapshot.ContextPressureReason.Should().Be(ChatHistoryPressureReasons.SummaryCompression);
        snapshot.ContextStrategy.Should().Be(ChatHistoryGovernanceStrategies.SummaryWithRecentSlice);
    }

    /// <summary>
    /// 历史压缩索引应进入任务编排事实源。
    /// </summary>
    [Fact]
    public void Build_ShouldCarryCompressionIndex_WhenHistoryWasCompressed()
    {
        var snapshot = AgentTaskOrchestrationSnapshotBuilder.Build(
            Guid.NewGuid(),
            agentLoopAttempt: 0,
            AgentLoopAction.None,
            new ChatHistoryGovernanceSnapshot
            {
                Strategy = ChatHistoryGovernanceStrategies.SummaryWithRecentSlice,
                SummaryMessageCount = 1,
                CompressionIndex = new ChatHistoryCompressionIndex
                {
                    HasIndex = true,
                    CoveredMessageCount = 8,
                    SummaryCharacterCount = 40,
                    SummaryFingerprint = "summary-hash",
                    TopicHints = ["上下文治理", "记忆沉淀"]
                }
            },
            null,
            null,
            []);

        snapshot.ContextCompressionIndex.HasIndex.Should().BeTrue();
        snapshot.ContextCompressionIndex.CoveredMessageCount.Should().Be(8);
        snapshot.ContextCompressionIndex.SummaryFingerprint.Should().Be("summary-hash");
        snapshot.ContextCompressionIndex.TopicHints.Should().Contain("上下文治理");
    }

    /// <summary>
    /// 无上下文压力时应保留默认压力原因。
    /// </summary>
    [Fact]
    public void Build_ShouldUseDefaultPressureReason_WhenNoPressureExists()
    {
        var snapshot = AgentTaskOrchestrationSnapshotBuilder.Build(
            Guid.NewGuid(),
            agentLoopAttempt: 0,
            AgentLoopAction.None,
            ChatHistoryGovernanceSnapshot.Empty,
            null,
            null,
            []);

        snapshot.HasContextPressure.Should().BeFalse();
        snapshot.ContextPressureReason.Should().Be(ChatHistoryPressureReasons.None);
    }

    /// <summary>
    /// 压力快照缺失时应从记忆触发决策兜底读取压力原因。
    /// </summary>
    [Fact]
    public void Build_ShouldUseMemoryPressureReason_WhenHistoryPressureIsMissing()
    {
        var snapshot = AgentTaskOrchestrationSnapshotBuilder.Build(
            Guid.NewGuid(),
            agentLoopAttempt: 0,
            AgentLoopAction.None,
            ChatHistoryGovernanceSnapshot.Empty,
            null,
            new MemoryConsolidationTriggerDecision
            {
                Reason = MemoryConsolidationTriggerReasons.ContextPressureDetected,
                ContextPressureReason = ChatHistoryPressureReasons.BudgetTruncated
            },
            []);

        snapshot.ContextPressureReason.Should().Be(ChatHistoryPressureReasons.BudgetTruncated);
    }

    /// <summary>
    /// 系统经验回放快照应进入任务编排事实源。
    /// </summary>
    [Fact]
    public void Build_ShouldCarrySystemExperienceReplaySnapshot()
    {
        var experienceId = Guid.NewGuid();
        var sourceSessionId = Guid.NewGuid();
        const string PromptFingerprint = "prompt-fingerprint";
        var snapshot = AgentTaskOrchestrationSnapshotBuilder.Build(
            Guid.NewGuid(),
            agentLoopAttempt: 0,
            AgentLoopAction.None,
            ChatHistoryGovernanceSnapshot.Empty,
            new SystemExperienceReplaySnapshot
            {
                HasMatch = true,
                InjectedDynamicContext = true,
                Reason = SystemExperienceReplayReasons.DynamicContext,
                ExperienceId = experienceId,
                Similarity = 0.88f,
                ContextTagSnapshot = new SystemExperienceContextTagSnapshot
                {
                    ValueSignalKeyword = "偏好",
                    SourceSessionId = sourceSessionId,
                    DistillationPromptFingerprint = PromptFingerprint
                }
            },
            null,
            []);

        snapshot.SystemExperienceReplay.InjectedDynamicContext.Should().BeTrue();
        snapshot.SystemExperienceReplay.ExperienceId.Should().Be(experienceId);
        snapshot.SystemExperienceReplay.Reason.Should().Be(SystemExperienceReplayReasons.DynamicContext);
        snapshot.ExperienceMemoryCitation.ExperienceId.Should().Be(experienceId);
        snapshot.ExperienceMemoryCitation.ValueSignalKeyword.Should().Be("偏好");
        snapshot.ExperienceMemoryCitation.SourceSessionId.Should().Be(sourceSessionId);
        snapshot.ExperienceMemoryCitation.DistillationPromptFingerprint.Should().Be(PromptFingerprint);
        snapshot.ExperienceMemoryCitation.CitationFingerprint.Should().NotBeEmpty();
        snapshot.ExperienceValueSignalKeyword.Should().Be("偏好");
        snapshot.ExperienceSourceSessionId.Should().Be(sourceSessionId);
        snapshot.ExperienceDistillationPromptFingerprint.Should().Be(PromptFingerprint);
    }

    /// <summary>
    /// 直接返回的系统经验也应被视为已回放。
    /// </summary>
    [Fact]
    public void Build_ShouldTreatDirectAnswerAsExperienceReplay()
    {
        var snapshot = AgentTaskOrchestrationSnapshotBuilder.Build(
            Guid.NewGuid(),
            agentLoopAttempt: 0,
            AgentLoopAction.None,
            ChatHistoryGovernanceSnapshot.Empty,
            new SystemExperienceReplaySnapshot
            {
                HasMatch = true,
                AnsweredDirectly = true,
                Reason = SystemExperienceReplayReasons.DirectAnswer
            },
            null,
            []);

        snapshot.SystemExperienceReplay.WasReplayed.Should().BeTrue();
        snapshot.SystemExperienceReplay.AnsweredDirectly.Should().BeTrue();
    }

    private static ToolExecutionRecord CreateRecord(
        string toolName,
        bool success,
        ToolFailureReason failureReason,
        ToolSuggestedAction suggestedAction)
    {
        return new ToolExecutionRecord
        {
            ToolCallId = Guid.NewGuid(),
            ToolName = toolName,
            Success = success,
            FailureReason = failureReason,
            SuggestedAction = suggestedAction
        };
    }
}
