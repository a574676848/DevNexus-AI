using DevNexus.Core.Models.Evaluation;
using DevNexus.Core.Services.Chat;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// Agent 单轮事件构建器测试。
/// </summary>
public sealed class AgentTurnEventBuilderTests
{
    /// <summary>
    /// 工具事件应保留原始顺序并使用一基序号。
    /// </summary>
    [Fact]
    public void FromToolRecords_ShouldPreserveOrderAndUseOneBasedSequence()
    {
        var turnId = Guid.NewGuid();
        var firstToolCallId = Guid.NewGuid();
        var secondToolCallId = Guid.NewGuid();
        var records = new[]
        {
            CreateRecord("Shell.Read", firstToolCallId, success: true),
            CreateRecord("Shell.Write", secondToolCallId, success: false)
        };

        var events = AgentTurnEventBuilder.FromToolRecords(turnId, records);

        events.Select(item => item.Sequence).Should().Equal(1, 2);
        events.Select(item => item.ToolCallId).Should().Equal(firstToolCallId, secondToolCallId);
        events.Select(item => item.ToolName).Should().Equal("Shell.Read", "Shell.Write");
    }

    /// <summary>
    /// 成功和失败工具应映射到不同事件类型。
    /// </summary>
    [Fact]
    public void FromToolRecords_ShouldMapToolStatusToEventKind()
    {
        var turnId = Guid.NewGuid();
        var records = new[]
        {
            CreateRecord("Shell.Read", Guid.NewGuid(), success: true),
            CreateRecord("Shell.Write", Guid.NewGuid(), success: false)
        };

        var events = AgentTurnEventBuilder.FromToolRecords(turnId, records);

        events.Select(item => item.Kind)
            .Should()
            .Equal(AgentTurnEventKind.ToolCompleted, AgentTurnEventKind.ToolFailed);
    }

    /// <summary>
    /// 工具事件应复用统一摘要语义。
    /// </summary>
    [Fact]
    public void FromToolRecords_ShouldReuseToolExecutionSummary()
    {
        var turnId = Guid.NewGuid();
        var record = CreateRecord(
            "Approval.Apply",
            Guid.NewGuid(),
            success: false,
            failureReason: ToolFailureReason.ApprovalRequired,
            suggestedAction: ToolSuggestedAction.RequestApproval);

        var turnEvent = AgentTurnEventBuilder.FromToolRecords(turnId, new[] { record }).Single();

        turnEvent.Title.Should().Be("工具等待审批");
        turnEvent.Message.Should().Be("当前操作需要审批后才能继续执行。");
        turnEvent.SuggestedAction.Should().Be(ToolSuggestedAction.RequestApproval);
    }

    /// <summary>
    /// 单轮事件批次 DTO 应保留轮次和事件字段。
    /// </summary>
    [Fact]
    public void BuildUpdatedDto_ShouldPreserveTurnAndEventFields()
    {
        var turnId = Guid.NewGuid();
        var toolCallId = Guid.NewGuid();
        var record = CreateRecord("Shell.Read", toolCallId, success: true);

        var dto = AgentTurnEventBuilder.BuildUpdatedDto(turnId, new[] { record });

        dto.TurnId.Should().Be(turnId);
        dto.Events.Should().ContainSingle();
        dto.Events[0].TurnId.Should().Be(turnId);
        dto.Events[0].ToolCallId.Should().Be(toolCallId);
        dto.Events[0].Kind.Should().Be(AgentTurnEventKind.ToolCompleted);
        dto.EventCount.Should().Be(1);
        dto.FailedEventCount.Should().Be(0);
        dto.EventBatchHash.Should().NotBeNullOrWhiteSpace();
        dto.BatchDiagnostics.HasFailures.Should().BeFalse();
        dto.BatchDiagnostics.CompletedEventCount.Should().Be(1);
        dto.BatchDiagnostics.FailedEventCount.Should().Be(0);
        dto.BatchDiagnostics.UniqueToolCount.Should().Be(1);
        dto.BatchDiagnostics.TotalDurationMs.Should().Be(0);
        dto.BatchDiagnostics.SlowestToolName.Should().BeNull();
        dto.BatchDiagnostics.SlowestDurationMs.Should().Be(0);
        dto.BatchDiagnostics.FirstFailedToolName.Should().BeNull();
        dto.BatchDiagnostics.FirstFailureSummary.Should().BeNull();
    }

    /// <summary>
    /// 事件批次指纹应按事件序号稳定生成。
    /// </summary>
    [Fact]
    public void BuildBatchHash_ShouldBeStable_WhenInputOrderChanges()
    {
        var turnId = Guid.NewGuid();
        var first = AgentTurnEventBuilder.FromToolRecords(
            turnId,
            [
                CreateRecord("Shell.Read", Guid.NewGuid(), success: true),
                CreateRecord("Shell.Write", Guid.NewGuid(), success: false)
            ]);
        var reordered = first.Reverse().ToArray();

        var firstHash = AgentTurnEventBuilder.BuildBatchHash(first);
        var reorderedHash = AgentTurnEventBuilder.BuildBatchHash(reordered);

        reorderedHash.Should().Be(firstHash);
    }

    /// <summary>
    /// 事件语义变化应改变批次指纹。
    /// </summary>
    [Fact]
    public void BuildBatchHash_ShouldChange_WhenEventMeaningChanges()
    {
        var turnId = Guid.NewGuid();
        var toolCallId = Guid.NewGuid();
        var first = AgentTurnEventBuilder.FromToolRecords(
            turnId,
            [CreateRecord("Shell.Read", toolCallId, success: true)]);
        var second = AgentTurnEventBuilder.FromToolRecords(
            turnId,
            [CreateRecord("Shell.Read", toolCallId, success: false)]);

        var firstHash = AgentTurnEventBuilder.BuildBatchHash(first);
        var secondHash = AgentTurnEventBuilder.BuildBatchHash(second);

        secondHash.Should().NotBe(firstHash);
    }

    /// <summary>
    /// 批次诊断应按事件序号稳定生成。
    /// </summary>
    [Fact]
    public void BuildBatchDiagnostics_ShouldBeStable_WhenInputOrderChanges()
    {
        var turnId = Guid.NewGuid();
        var events = AgentTurnEventBuilder.FromToolRecords(
            turnId,
            [
                CreateRecord("Shell.Read", Guid.NewGuid(), success: true),
                CreateRecord(
                    "Approval.Apply",
                    Guid.NewGuid(),
                    success: false,
                    failureReason: ToolFailureReason.ApprovalRequired,
                    suggestedAction: ToolSuggestedAction.RequestApproval),
                CreateRecord("Shell.Read", Guid.NewGuid(), success: false, suggestedAction: ToolSuggestedAction.Retry)
            ]);

        var diagnostics = AgentTurnEventBuilder.BuildBatchDiagnostics(events.Reverse().ToArray());

        diagnostics.HasFailures.Should().BeTrue();
        diagnostics.FirstSequence.Should().Be(1);
        diagnostics.LastSequence.Should().Be(3);
        diagnostics.CompletedEventCount.Should().Be(1);
        diagnostics.FailedEventCount.Should().Be(2);
        diagnostics.UniqueToolCount.Should().Be(2);
        diagnostics.TotalDurationMs.Should().Be(0);
        diagnostics.FirstFailedSequence.Should().Be(2);
        diagnostics.FirstFailedToolName.Should().Be("Approval.Apply");
        diagnostics.FirstFailureSummary.Should().Be("当前操作需要审批后才能继续执行。");
        diagnostics.PrimarySuggestedAction.Should().Be(ToolSuggestedAction.RequestApproval);
        diagnostics.PrimarySuggestedActionText.Should().Be("等待审批");
    }

    /// <summary>
    /// 批次诊断应透出首个失败摘要。
    /// </summary>
    [Fact]
    public void BuildUpdatedDto_ShouldExposeFirstFailureSummary()
    {
        var turnId = Guid.NewGuid();
        var records = new[]
        {
            CreateRecord("Shell.Read", Guid.NewGuid(), success: true),
            CreateRecord(
                "Approval.Apply",
                Guid.NewGuid(),
                success: false,
                failureReason: ToolFailureReason.ApprovalRequired,
                suggestedAction: ToolSuggestedAction.RequestApproval),
            CreateRecord("Shell.Write", Guid.NewGuid(), success: false)
        };

        var dto = AgentTurnEventBuilder.BuildUpdatedDto(turnId, records);

        dto.BatchDiagnostics.FirstFailedSequence.Should().Be(2);
        dto.BatchDiagnostics.FirstFailedToolName.Should().Be("Approval.Apply");
        dto.BatchDiagnostics.FirstFailureSummary.Should().Be("当前操作需要审批后才能继续执行。");
        dto.BatchDiagnostics.PrimarySuggestedActionText.Should().Be("等待审批");
    }

    /// <summary>
    /// 批次诊断应汇总工具执行总耗时。
    /// </summary>
    [Fact]
    public void BuildUpdatedDto_ShouldSummarizeTotalDuration()
    {
        var turnId = Guid.NewGuid();
        var records = new[]
        {
            CreateRecord("Shell.Read", Guid.NewGuid(), success: true, durationMs: 120),
            CreateRecord("Shell.Write", Guid.NewGuid(), success: false, durationMs: 380)
        };

        var dto = AgentTurnEventBuilder.BuildUpdatedDto(turnId, records);

        dto.BatchDiagnostics.TotalDurationMs.Should().Be(500);
        dto.BatchDiagnostics.SlowestToolName.Should().Be("Shell.Write");
        dto.BatchDiagnostics.SlowestDurationMs.Should().Be(380);
    }

    /// <summary>
    /// 最慢工具耗时相同时应按工具名称稳定选择。
    /// </summary>
    [Fact]
    public void BuildUpdatedDto_ShouldResolveSlowestToolDeterministically_WhenDurationTies()
    {
        var turnId = Guid.NewGuid();
        var records = new[]
        {
            CreateRecord("Shell.Write", Guid.NewGuid(), success: true, durationMs: 200),
            CreateRecord("Shell.Read", Guid.NewGuid(), success: true, durationMs: 200)
        };

        var dto = AgentTurnEventBuilder.BuildUpdatedDto(turnId, records);

        dto.BatchDiagnostics.SlowestToolName.Should().Be("Shell.Read");
        dto.BatchDiagnostics.SlowestDurationMs.Should().Be(200);
    }

    /// <summary>
    /// 批次诊断应复用工具恢复策略推导缺失的建议动作。
    /// </summary>
    [Fact]
    public void BuildUpdatedDto_ShouldInferPrimarySuggestedAction_WhenExplicitActionIsMissing()
    {
        var turnId = Guid.NewGuid();
        var records = new[]
        {
            CreateRecord(
                "Provider.Refresh",
                Guid.NewGuid(),
                success: false,
                failureReason: ToolFailureReason.AuthExpired,
                shouldRotateCredential: true),
            CreateRecord(
                "Shell.Read",
                Guid.NewGuid(),
                success: false,
                retryable: true)
        };

        var dto = AgentTurnEventBuilder.BuildUpdatedDto(turnId, records);

        dto.BatchDiagnostics.PrimarySuggestedAction.Should().Be(ToolSuggestedAction.RefreshCredential);
        dto.BatchDiagnostics.PrimarySuggestedActionText.Should().Be("刷新凭证");
    }

    /// <summary>
    /// 批次诊断应在无事件时保持空摘要。
    /// </summary>
    [Fact]
    public void BuildBatchDiagnostics_ShouldReturnEmptySummary_WhenEventsAreEmpty()
    {
        var diagnostics = AgentTurnEventBuilder.BuildBatchDiagnostics([]);

        diagnostics.HasFailures.Should().BeFalse();
        diagnostics.FirstSequence.Should().Be(0);
        diagnostics.LastSequence.Should().Be(0);
        diagnostics.CompletedEventCount.Should().Be(0);
        diagnostics.FailedEventCount.Should().Be(0);
        diagnostics.UniqueToolCount.Should().Be(0);
        diagnostics.TotalDurationMs.Should().Be(0);
        diagnostics.SlowestToolName.Should().BeNull();
        diagnostics.SlowestDurationMs.Should().Be(0);
        diagnostics.FirstFailedSequence.Should().Be(0);
        diagnostics.FirstFailedToolName.Should().BeNull();
        diagnostics.FirstFailureSummary.Should().BeNull();
        diagnostics.PrimarySuggestedAction.Should().Be(ToolSuggestedAction.None);
        diagnostics.PrimarySuggestedActionText.Should().Be("无需动作");
    }

    private static ToolExecutionRecord CreateRecord(
        string toolName,
        Guid toolCallId,
        bool success,
        ToolFailureReason failureReason = ToolFailureReason.Unknown,
        ToolSuggestedAction suggestedAction = ToolSuggestedAction.None,
        int durationMs = 0,
        bool retryable = false,
        bool shouldRotateCredential = false)
    {
        return new ToolExecutionRecord
        {
            ToolCallId = toolCallId,
            ToolName = toolName,
            Success = success,
            FailureReason = success ? ToolFailureReason.None : failureReason,
            Retryable = retryable,
            ShouldRotateCredential = shouldRotateCredential,
            SuggestedAction = suggestedAction,
            Output = success ? "执行完成" : null,
            Duration = TimeSpan.FromMilliseconds(durationMs)
        };
    }
}
