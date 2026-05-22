using DevNexus.Core.Services.Chat;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 记忆沉淀触发策略测试。
/// </summary>
public sealed class MemoryConsolidationTriggerPolicyTests
{
    /// <summary>
    /// 消息增量达到阈值时应立即沉淀。
    /// </summary>
    [Fact]
    public void Decide_ShouldEnqueueImmediately_WhenMessageThresholdReached()
    {
        var decision = MemoryConsolidationTriggerPolicy.Decide(
            currentMessageCount: 12,
            lastConsolidatedMessageCount: 2,
            messageThreshold: 10,
            minimumDelayedMessageCount: 3,
            historyGovernance: null,
            hasExistingJob: true);

        decision.ShouldEnqueueImmediately.Should().BeTrue();
        decision.ShouldCancelExistingJob.Should().BeTrue();
        decision.Reason.Should().Be(MemoryConsolidationTriggerReasons.MessageThresholdReached);
    }

    /// <summary>
    /// 出现上下文压缩压力时，即使消息增量未达阈值也应立即沉淀。
    /// </summary>
    [Fact]
    public void Decide_ShouldEnqueueImmediately_WhenContextPressureDetected()
    {
        var decision = MemoryConsolidationTriggerPolicy.Decide(
            currentMessageCount: 5,
            lastConsolidatedMessageCount: 2,
            messageThreshold: 10,
            minimumDelayedMessageCount: 3,
            historyGovernance: new ChatHistoryGovernanceSnapshot
            {
                Strategy = ChatHistoryGovernanceStrategies.SummaryWithRecentSlice,
                CompressedMessageCount = 4,
                SummaryMessageCount = 1
            },
            hasExistingJob: false);

        decision.ShouldEnqueueImmediately.Should().BeTrue();
        decision.Reason.Should().Be(MemoryConsolidationTriggerReasons.ContextPressureDetected);
        decision.ContextPressureReason.Should().Be(ChatHistoryPressureReasons.SummaryCompression);
    }

    /// <summary>
    /// 已沉淀过的摘要压缩窗口不应反复触发立即沉淀。
    /// </summary>
    [Fact]
    public void Decide_ShouldScheduleDelayed_WhenSummaryCompressionWindowWasConsolidated()
    {
        var decision = MemoryConsolidationTriggerPolicy.Decide(
            currentMessageCount: 7,
            lastConsolidatedMessageCount: 6,
            messageThreshold: 10,
            minimumDelayedMessageCount: 3,
            historyGovernance: new ChatHistoryGovernanceSnapshot
            {
                Strategy = ChatHistoryGovernanceStrategies.SummaryWithRecentSlice,
                CompressedMessageCount = 5,
                SummaryMessageCount = 1
            },
            hasExistingJob: true);

        decision.ShouldEnqueueImmediately.Should().BeFalse();
        decision.ShouldScheduleDelayed.Should().BeTrue();
        decision.ShouldCancelExistingJob.Should().BeTrue();
        decision.Reason.Should().Be(MemoryConsolidationTriggerReasons.IdleDelayScheduled);
    }

    /// <summary>
    /// 普通新增消息应调度空闲延迟沉淀。
    /// </summary>
    [Fact]
    public void Decide_ShouldScheduleDelayed_WhenMessageCountIsEnough()
    {
        var decision = MemoryConsolidationTriggerPolicy.Decide(
            currentMessageCount: 4,
            lastConsolidatedMessageCount: 2,
            messageThreshold: 10,
            minimumDelayedMessageCount: 3,
            historyGovernance: ChatHistoryGovernanceSnapshot.Empty,
            hasExistingJob: true);

        decision.ShouldScheduleDelayed.Should().BeTrue();
        decision.ShouldCancelExistingJob.Should().BeTrue();
        decision.Reason.Should().Be(MemoryConsolidationTriggerReasons.IdleDelayScheduled);
    }

    /// <summary>
    /// 消息数不足时不应调度沉淀任务。
    /// </summary>
    [Fact]
    public void Decide_ShouldSkip_WhenTooFewMessages()
    {
        var decision = MemoryConsolidationTriggerPolicy.Decide(
            currentMessageCount: 2,
            lastConsolidatedMessageCount: 0,
            messageThreshold: 10,
            minimumDelayedMessageCount: 3,
            historyGovernance: ChatHistoryGovernanceSnapshot.Empty,
            hasExistingJob: false);

        decision.ShouldEnqueueImmediately.Should().BeFalse();
        decision.ShouldScheduleDelayed.Should().BeFalse();
        decision.Reason.Should().Be(MemoryConsolidationTriggerReasons.TooFewMessages);
    }
}
