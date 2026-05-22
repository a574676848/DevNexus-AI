using DevNexus.Core.Models.Evaluation;
using DevNexus.Core.Services.Chat;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 工具执行事件摘要构建器测试。
/// </summary>
public sealed class ToolExecutionEventSummaryBuilderTests
{
    /// <summary>
    /// 成功工具应生成完成事件。
    /// </summary>
    [Fact]
    public void Build_ShouldCreateCompletedSummary_WhenToolSucceeded()
    {
        var record = CreateRecord(success: true, output: "执行完成");

        var summary = ToolExecutionEventSummaryBuilder.Build(record);

        summary.Title.Should().Be("工具执行完成");
        summary.Message.Should().Be("执行完成");
        summary.SuggestedAction.Should().Be(ToolSuggestedAction.None);
    }

    /// <summary>
    /// 审批类失败应生成等待审批事件。
    /// </summary>
    [Fact]
    public void Build_ShouldCreateApprovalSummary_WhenApprovalRequired()
    {
        var record = CreateRecord(
            failureReason: ToolFailureReason.ApprovalRequired,
            suggestedAction: ToolSuggestedAction.RequestApproval);

        var summary = ToolExecutionEventSummaryBuilder.Build(record);

        summary.Title.Should().Be("工具等待审批");
        summary.Message.Should().Be("当前操作需要审批后才能继续执行。");
    }

    /// <summary>
    /// 失败摘要应优先使用用户可读消息。
    /// </summary>
    [Fact]
    public void Build_ShouldPreferUserMessage()
    {
        var record = CreateRecord(
            userMessage: "请补充查询关键词。",
            errorSummary: "内部错误摘要");

        var summary = ToolExecutionEventSummaryBuilder.Build(record);

        summary.Message.Should().Be("请补充查询关键词。");
    }

    /// <summary>
    /// 多工具失败摘要应带工具名、去重并限制数量。
    /// </summary>
    [Fact]
    public void BuildFailureDigest_ShouldDeduplicateAndLimitMessages()
    {
        var records = new[]
        {
            CreateRecord(toolName: "HostService.ExecuteCommand", userMessage: "失败一"),
            CreateRecord(toolName: "HostService.ExecuteCommand", userMessage: "失败一"),
            CreateRecord(toolName: "Knowledge.Search", userMessage: "失败二"),
            CreateRecord(toolName: "Web.Search", userMessage: "失败三")
        };

        var digest = ToolExecutionEventSummaryBuilder.BuildFailureDigest(records);

        digest.Should().Be("HostService.ExecuteCommand: 失败一；Knowledge.Search: 失败二");
    }

    /// <summary>
    /// 长工具输出摘要应保留头尾和输出规模。
    /// </summary>
    [Fact]
    public void Build_ShouldCompressLongOutputWithBudgetMetadata()
    {
        var record = CreateRecord(
            success: true,
            output: "BEGIN-" + new string('a', 260) + "-TAIL");

        var summary = ToolExecutionEventSummaryBuilder.Build(record);

        summary.Message.Should().Contain("Total output chars:");
        summary.Message.Should().Contain("BEGIN-");
        summary.Message.Should().Contain("-TAIL");
        summary.Message.Length.Should().BeLessThanOrEqualTo(180);
    }

    private static ToolExecutionRecord CreateRecord(
        bool success = false,
        string toolName = "HostService.ExecuteCommand",
        string output = "",
        string? userMessage = null,
        string? errorSummary = null,
        ToolFailureReason failureReason = ToolFailureReason.Unknown,
        ToolSuggestedAction suggestedAction = ToolSuggestedAction.None)
    {
        return new ToolExecutionRecord
        {
            ToolName = toolName,
            Success = success,
            Output = output,
            UserMessage = userMessage,
            ErrorSummary = errorSummary,
            FailureReason = success ? ToolFailureReason.None : failureReason,
            SuggestedAction = suggestedAction
        };
    }
}
