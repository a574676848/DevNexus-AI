using DevNexus.Core.Models.Evaluation;
using DevNexus.Core.Services.Chat;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 工具恢复策略摘要构建器测试。
/// </summary>
public sealed class ToolRecoveryStrategySummaryBuilderTests
{
    /// <summary>
    /// 无失败工具时应返回完成摘要。
    /// </summary>
    [Fact]
    public void Build_ShouldReturnCompletedSummary_WhenAllToolsSucceeded()
    {
        var records = new[]
        {
            CreateRecord(success: true, suggestedAction: ToolSuggestedAction.None)
        };

        var summary = ToolRecoveryStrategySummaryBuilder.Build(records);

        summary.HasFailures.Should().BeFalse();
        summary.PrimaryAction.Should().Be(ToolSuggestedAction.None);
        summary.Title.Should().Be("工具执行完成");
    }

    /// <summary>
    /// 多个失败动作应按确定性恢复优先级排序。
    /// </summary>
    [Fact]
    public void Build_ShouldOrderActionsByRecoveryPriority()
    {
        var records = new[]
        {
            CreateRecord(suggestedAction: ToolSuggestedAction.Retry),
            CreateRecord(suggestedAction: ToolSuggestedAction.RequestApproval),
            CreateRecord(suggestedAction: ToolSuggestedAction.Fallback)
        };

        var summary = ToolRecoveryStrategySummaryBuilder.Build(records);

        summary.PrimaryAction.Should().Be(ToolSuggestedAction.RequestApproval);
        summary.OrderedActions.Should().Equal(
            ToolSuggestedAction.RequestApproval,
            ToolSuggestedAction.Retry,
            ToolSuggestedAction.Fallback);
        summary.Title.Should().Be("工具恢复需要审批");
    }

    /// <summary>
    /// 缺少显式建议动作时应从记录标志推导恢复动作。
    /// </summary>
    [Fact]
    public void Build_ShouldInferAction_WhenSuggestedActionMissing()
    {
        var records = new[]
        {
            CreateRecord(
                suggestedAction: ToolSuggestedAction.None,
                shouldRotateCredential: true),
            CreateRecord(
                suggestedAction: ToolSuggestedAction.None,
                retryable: true)
        };

        var summary = ToolRecoveryStrategySummaryBuilder.Build(records);

        summary.OrderedActions.Should().Equal(
            ToolSuggestedAction.RefreshCredential,
            ToolSuggestedAction.Retry);
        summary.Message.Should().StartWith("请先刷新或补充凭证");
    }

    /// <summary>
    /// 仍在运行的终端命令应进入等待完成策略，而不是普通重试或降级。
    /// </summary>
    [Fact]
    public void Build_ShouldUseWaitForCompletion_WhenCommandStillRunning()
    {
        var records = new[]
        {
            CreateRecord(
                failureReason: ToolFailureReason.None,
                suggestedAction: ToolSuggestedAction.WaitForCompletion,
                retryable: true)
        };

        var summary = ToolRecoveryStrategySummaryBuilder.Build(records);

        summary.PrimaryAction.Should().Be(ToolSuggestedAction.WaitForCompletion);
        summary.Title.Should().Be("工具执行仍在运行");
        summary.Message.Should().StartWith("请等待同一终端会话完成");
    }

    /// <summary>
    /// 停止命令未闭环时应进入同会话停止策略，而不是降级。
    /// </summary>
    [Fact]
    public void Build_ShouldUseStopCommand_WhenStopIsRecommended()
    {
        var records = new[]
        {
            CreateRecord(
                failureReason: ToolFailureReason.None,
                suggestedAction: ToolSuggestedAction.StopCommand,
                retryable: true)
        };

        var summary = ToolRecoveryStrategySummaryBuilder.Build(records);

        summary.PrimaryAction.Should().Be(ToolSuggestedAction.StopCommand);
        summary.Title.Should().Be("工具恢复需要停止终端命令");
        summary.Message.Should().StartWith("请停止同一终端会话");
    }

    /// <summary>
    /// 挂起交互工具应遵循恢复策略优先级。
    /// </summary>
    [Fact]
    public void SelectPendingInteractionTool_ShouldUseStrategyPriority()
    {
        var retryTool = CreateRecord(
            toolName: "RetryTool",
            suggestedAction: ToolSuggestedAction.PromptUserInput,
            requiresHumanIntervention: true);
        var approvalTool = CreateRecord(
            toolName: "ApprovalTool",
            suggestedAction: ToolSuggestedAction.RequestApproval,
            requiresHumanIntervention: true);
        var records = new[] { retryTool, approvalTool };
        var summary = ToolRecoveryStrategySummaryBuilder.Build(records);

        var selected = ToolRecoveryStrategySummaryBuilder.SelectPendingInteractionTool(records, summary);

        selected.Should().NotBeNull();
        selected!.ToolName.Should().Be("ApprovalTool");
    }

    private static ToolExecutionRecord CreateRecord(
        bool success = false,
        string toolName = "HostService.ExecuteCommand",
        ToolSuggestedAction suggestedAction = ToolSuggestedAction.Fallback,
        ToolFailureReason failureReason = ToolFailureReason.Unknown,
        bool retryable = false,
        bool requiresHumanIntervention = false,
        bool shouldFallback = false,
        bool shouldRotateCredential = false)
    {
        return new ToolExecutionRecord
        {
            ToolCallId = Guid.NewGuid(),
            ToolName = toolName,
            Arguments = "{\"command\":\"dotnet build\"}",
            Success = success,
            FailureReason = success ? ToolFailureReason.None : failureReason,
            Retryable = retryable,
            RequiresHumanIntervention = requiresHumanIntervention,
            ShouldFallback = shouldFallback,
            ShouldRotateCredential = shouldRotateCredential,
            SuggestedAction = suggestedAction,
            UserMessage = success ? null : "工具失败"
        };
    }
}
