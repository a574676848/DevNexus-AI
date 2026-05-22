using DevNexus.Core.Services.Chat;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 自我迭代候选策略测试。
/// </summary>
public sealed class SelfIterationCandidatePolicyTests
{
    /// <summary>
    /// Agent Loop 重试中不应触发经验提纯。
    /// </summary>
    [Fact]
    public void Decide_ShouldObserveOnly_WhenAgentLoopIsRetrying()
    {
        var decision = SelfIterationCandidatePolicy.Decide(new AgentTaskOrchestrationSnapshot
        {
            AgentLoopAction = AgentLoopAction.Retry
        });

        decision.ShouldDistillExperience.Should().BeFalse();
        decision.ShouldObserveOnly.Should().BeTrue();
        decision.Reason.Should().Be(SelfIterationCandidateReasons.AgentLoopRetrying);
    }

    /// <summary>
    /// 工具恢复未处理时不应触发经验提纯。
    /// </summary>
    [Fact]
    public void Decide_ShouldObserveOnly_WhenToolRecoveryIsPending()
    {
        var decision = SelfIterationCandidatePolicy.Decide(new AgentTaskOrchestrationSnapshot
        {
            AgentLoopAction = AgentLoopAction.None,
            FailedToolEventCount = 1,
            PrimarySuggestedAction = ToolSuggestedAction.RequestApproval
        });

        decision.ShouldDistillExperience.Should().BeFalse();
        decision.Reason.Should().Be(SelfIterationCandidateReasons.ToolRecoveryPending);
    }

    /// <summary>
    /// 上下文压力被解决后应触发经验提纯。
    /// </summary>
    [Fact]
    public void Decide_ShouldDistill_WhenContextPressureWasResolved()
    {
        var decision = SelfIterationCandidatePolicy.Decide(new AgentTaskOrchestrationSnapshot
        {
            AgentLoopAction = AgentLoopAction.None,
            HasContextPressure = true
        });

        decision.ShouldDistillExperience.Should().BeTrue();
        decision.Reason.Should().Be(SelfIterationCandidateReasons.ContextPressureResolved);
        decision.ContextPressureReason.Should().Be(ChatHistoryPressureReasons.None);
    }

    /// <summary>
    /// 复用既有系统经验时不应重复提纯新经验。
    /// </summary>
    [Fact]
    public void Decide_ShouldObserveOnly_WhenSystemExperienceWasReused()
    {
        var decision = SelfIterationCandidatePolicy.Decide(new AgentTaskOrchestrationSnapshot
        {
            AgentLoopAction = AgentLoopAction.None,
            SystemExperienceReplay = new SystemExperienceReplaySnapshot
            {
                HasMatch = true,
                InjectedDynamicContext = true,
                Reason = SystemExperienceReplayReasons.DynamicContext
            },
            ResponseLength = SelfIterationCandidatePolicy.MinimumResponseLengthForDistillation
        });

        decision.ShouldDistillExperience.Should().BeFalse();
        decision.ShouldObserveOnly.Should().BeTrue();
        decision.Reason.Should().Be(SelfIterationCandidateReasons.SystemExperienceReused);
    }

    /// <summary>
    /// 复用系统经验时应保留该经验沉淀时的自我迭代来源事实。
    /// </summary>
    [Fact]
    public void Decide_ShouldCarryReusedExperienceFacts_WhenSystemExperienceWasReused()
    {
        var sourceSessionId = Guid.NewGuid();
        const string PromptFingerprint = "prompt-fingerprint";
        var decision = SelfIterationCandidatePolicy.Decide(new AgentTaskOrchestrationSnapshot
        {
            AgentLoopAction = AgentLoopAction.None,
            SystemExperienceReplay = new SystemExperienceReplaySnapshot
            {
                HasMatch = true,
                InjectedDynamicContext = true,
                Reason = SystemExperienceReplayReasons.DynamicContext,
                ContextTagSnapshot = new SystemExperienceContextTagSnapshot
                {
                    CandidateReason = SelfIterationCandidateReasons.SummaryCompressionResolved,
                    ContextPressureReason = ChatHistoryPressureReasons.SummaryCompression,
                    ContextCompressionSummaryFingerprint = "summary-fingerprint",
                    ValueSignalKeyword = "架构",
                    SourceSessionId = sourceSessionId,
                    DistillationPromptFingerprint = PromptFingerprint
                }
            },
            ResponseLength = SelfIterationCandidatePolicy.MinimumResponseLengthForDistillation
        });

        decision.ShouldDistillExperience.Should().BeFalse();
        decision.ShouldObserveOnly.Should().BeTrue();
        decision.Reason.Should().Be(SelfIterationCandidateReasons.SystemExperienceReused);
        decision.ReusedExperienceHasSelfIterationFacts.Should().BeTrue();
        decision.ReusedExperienceCandidateReason.Should().Be(SelfIterationCandidateReasons.SummaryCompressionResolved);
        decision.ReusedExperienceContextPressureReason.Should().Be(ChatHistoryPressureReasons.SummaryCompression);
        decision.ReusedExperienceContextCompressionSummaryFingerprint.Should().Be("summary-fingerprint");
        decision.ReusedExperienceMemoryCitation.ValueSignalKeyword.Should().Be("架构");
        decision.ReusedExperienceMemoryCitation.SourceSessionId.Should().Be(sourceSessionId);
        decision.ReusedExperienceMemoryCitation.DistillationPromptFingerprint.Should().Be(PromptFingerprint);
        decision.ReusedExperienceMemoryCitation.CitationFingerprint.Should().NotBeEmpty();
        decision.ReusedExperienceValueSignalKeyword.Should().Be("架构");
        decision.ReusedExperienceSourceSessionId.Should().Be(sourceSessionId);
        decision.ReusedExperienceDistillationPromptFingerprint.Should().Be(PromptFingerprint);
    }

    /// <summary>
    /// 直接命中系统经验时也不应重复提纯。
    /// </summary>
    [Fact]
    public void Decide_ShouldObserveOnly_WhenSystemExperienceAnsweredDirectly()
    {
        var decision = SelfIterationCandidatePolicy.Decide(new AgentTaskOrchestrationSnapshot
        {
            AgentLoopAction = AgentLoopAction.None,
            SystemExperienceReplay = new SystemExperienceReplaySnapshot
            {
                HasMatch = true,
                AnsweredDirectly = true,
                Reason = SystemExperienceReplayReasons.DirectAnswer
            },
            ResponseLength = SelfIterationCandidatePolicy.MinimumResponseLengthForDistillation
        });

        decision.ShouldDistillExperience.Should().BeFalse();
        decision.ShouldObserveOnly.Should().BeTrue();
        decision.Reason.Should().Be(SelfIterationCandidateReasons.SystemExperienceReused);
    }

    /// <summary>
    /// 摘要压缩压力解决后应使用细分提纯原因。
    /// </summary>
    [Fact]
    public void Decide_ShouldDistillWithSummaryReason_WhenSummaryCompressionResolved()
    {
        var decision = SelfIterationCandidatePolicy.Decide(new AgentTaskOrchestrationSnapshot
        {
            AgentLoopAction = AgentLoopAction.None,
            HasContextPressure = true,
            ContextPressureReason = ChatHistoryPressureReasons.SummaryCompression
        });

        decision.ShouldDistillExperience.Should().BeTrue();
        decision.Reason.Should().Be(SelfIterationCandidateReasons.SummaryCompressionResolved);
        decision.ContextPressureReason.Should().Be(ChatHistoryPressureReasons.SummaryCompression);
    }

    /// <summary>
    /// 自我迭代候选应带出上下文压缩摘要指纹，便于复盘追踪同一次压缩事实。
    /// </summary>
    [Fact]
    public void Decide_ShouldCarryCompressionFingerprint_WhenContextPressureWasCompressed()
    {
        var decision = SelfIterationCandidatePolicy.Decide(new AgentTaskOrchestrationSnapshot
        {
            AgentLoopAction = AgentLoopAction.None,
            HasContextPressure = true,
            ContextPressureReason = ChatHistoryPressureReasons.SummaryCompression,
            ContextCompressionIndex = new ChatHistoryCompressionIndex
            {
                HasIndex = true,
                SummaryFingerprint = "summary-fingerprint"
            }
        });

        decision.ShouldDistillExperience.Should().BeTrue();
        decision.ContextPressureReason.Should().Be(ChatHistoryPressureReasons.SummaryCompression);
        decision.ContextCompressionSummaryFingerprint.Should().Be("summary-fingerprint");
    }

    /// <summary>
    /// 预算截断压力解决后应使用细分提纯原因。
    /// </summary>
    [Fact]
    public void Decide_ShouldDistillWithBudgetReason_WhenBudgetTruncationResolved()
    {
        var decision = SelfIterationCandidatePolicy.Decide(new AgentTaskOrchestrationSnapshot
        {
            AgentLoopAction = AgentLoopAction.None,
            HasContextPressure = true,
            ContextPressureReason = ChatHistoryPressureReasons.BudgetTruncated
        });

        decision.ShouldDistillExperience.Should().BeTrue();
        decision.Reason.Should().Be(SelfIterationCandidateReasons.BudgetTruncationResolved);
    }

    /// <summary>
    /// 未完成助手消息压力解决后应使用细分提纯原因。
    /// </summary>
    [Fact]
    public void Decide_ShouldDistillWithIncompleteAssistantReason_WhenSkippedTurnResolved()
    {
        var decision = SelfIterationCandidatePolicy.Decide(new AgentTaskOrchestrationSnapshot
        {
            AgentLoopAction = AgentLoopAction.None,
            HasContextPressure = true,
            ContextPressureReason = ChatHistoryPressureReasons.IncompleteAssistantSkipped
        });

        decision.ShouldDistillExperience.Should().BeTrue();
        decision.Reason.Should().Be(SelfIterationCandidateReasons.IncompleteAssistantSkippedResolved);
    }

    /// <summary>
    /// 完成的工具工作流应触发经验提纯。
    /// </summary>
    [Fact]
    public void Decide_ShouldDistill_WhenToolWorkflowCompleted()
    {
        var decision = SelfIterationCandidatePolicy.Decide(new AgentTaskOrchestrationSnapshot
        {
            AgentLoopAction = AgentLoopAction.None,
            ToolEventCount = 2,
            PrimarySuggestedAction = ToolSuggestedAction.None
        });

        decision.ShouldDistillExperience.Should().BeTrue();
        decision.Reason.Should().Be(SelfIterationCandidateReasons.ToolWorkflowCompleted);
    }

    /// <summary>
    /// 长回复完成后应保留普通 QA 经验提纯入口。
    /// </summary>
    [Fact]
    public void Decide_ShouldDistill_WhenLongFormAnswerCompleted()
    {
        var decision = SelfIterationCandidatePolicy.Decide(new AgentTaskOrchestrationSnapshot
        {
            AgentLoopAction = AgentLoopAction.None,
            ResponseLength = SelfIterationCandidatePolicy.MinimumResponseLengthForDistillation
        });

        decision.ShouldDistillExperience.Should().BeTrue();
        decision.Reason.Should().Be(SelfIterationCandidateReasons.LongFormAnswerCompleted);
    }

    /// <summary>
    /// 缺少有效信号时只观察不提纯。
    /// </summary>
    [Fact]
    public void Decide_ShouldObserveOnly_WhenNoSignalExists()
    {
        var decision = SelfIterationCandidatePolicy.Decide(new AgentTaskOrchestrationSnapshot
        {
            AgentLoopAction = AgentLoopAction.None,
            ResponseLength = SelfIterationCandidatePolicy.MinimumResponseLengthForDistillation - 1
        });

        decision.ShouldDistillExperience.Should().BeFalse();
        decision.ShouldObserveOnly.Should().BeTrue();
        decision.Reason.Should().Be(SelfIterationCandidateReasons.CompletedWithoutSignal);
        decision.ContextCompressionSummaryFingerprint.Should().BeEmpty();
    }
}
