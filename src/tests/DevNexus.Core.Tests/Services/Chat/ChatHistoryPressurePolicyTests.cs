using DevNexus.Core.Services.Chat;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 聊天历史上下文压力策略测试。
/// </summary>
public sealed class ChatHistoryPressurePolicyTests
{
    /// <summary>
    /// 摘要压缩应标记上下文压力。
    /// </summary>
    [Fact]
    public void Summarize_ShouldDetectPressure_WhenSummaryExists()
    {
        var summary = ChatHistoryPressurePolicy.Summarize(new ChatHistoryGovernanceSnapshot
        {
            SummaryMessageCount = 1
        });

        summary.HasPressure.Should().BeTrue();
        summary.PrimaryReason.Should().Be(ChatHistoryPressureReasons.SummaryCompression);
    }

    /// <summary>
    /// 预算截断应标记上下文压力。
    /// </summary>
    [Fact]
    public void Summarize_ShouldDetectPressure_WhenBudgetTruncated()
    {
        var summary = ChatHistoryPressurePolicy.Summarize(new ChatHistoryGovernanceSnapshot
        {
            TruncatedByBudget = true
        });

        summary.HasPressure.Should().BeTrue();
        summary.PrimaryReason.Should().Be(ChatHistoryPressureReasons.BudgetTruncated);
    }

    /// <summary>
    /// 跳过未完成助手消息应标记上下文压力。
    /// </summary>
    [Fact]
    public void Summarize_ShouldDetectPressure_WhenIncompleteAssistantSkipped()
    {
        var summary = ChatHistoryPressurePolicy.Summarize(new ChatHistoryGovernanceSnapshot
        {
            SkippedIncompleteAssistantMessageCount = 1
        });

        summary.HasPressure.Should().BeTrue();
        summary.PrimaryReason.Should().Be(ChatHistoryPressureReasons.IncompleteAssistantSkipped);
    }

    /// <summary>
    /// 无治理快照时不应标记上下文压力。
    /// </summary>
    [Fact]
    public void Summarize_ShouldReturnNoPressure_WhenSnapshotIsNull()
    {
        var summary = ChatHistoryPressurePolicy.Summarize(null);

        summary.HasPressure.Should().BeFalse();
        summary.PrimaryReason.Should().Be(ChatHistoryPressureReasons.None);
    }
}
