using DevNexus.Core.Models.Evaluation;
using DevNexus.Core.Services.Chat;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 上下文溢出恢复提示构建器测试。
/// </summary>
public sealed class ContextOverflowRepairPromptBuilderTests
{
    /// <summary>
    /// 没有上下文溢出失败时不应生成恢复提示。
    /// </summary>
    [Fact]
    public void TryBuild_ShouldReturnNull_WhenNoContextOverflowFailure()
    {
        var records = new[]
        {
            CreateRecord(
                ToolFailureReason.TransientNetworkFailure,
                userMessage: "connection refused")
        };

        var prompt = ContextOverflowRepairPromptBuilder.TryBuild("构建项目", records);

        prompt.Should().BeNull();
    }

    /// <summary>
    /// 上下文溢出失败应生成片段化的稳定恢复提示。
    /// </summary>
    [Fact]
    public void TryBuild_ShouldBuildStableFragmentPrompt_WhenContextOverflowDetected()
    {
        var records = new[]
        {
            CreateRecord(
                ToolFailureReason.ContextOverflow,
                userMessage: "prompt is too long")
        };

        var prompt = ContextOverflowRepairPromptBuilder.TryBuild("分析大型日志", records);

        prompt.Should().NotBeNull();
        prompt.Should().Contain("## 上下文溢出恢复指令");
        prompt.Should().Contain("### 原始目标");
        prompt.Should().Contain("分析大型日志");
        prompt.Should().Contain("### 失败摘要");
        prompt.Should().Contain("HostService.ExecuteCommand: failureReason=ContextOverflow");
        prompt.Should().Contain("suggestedAction=Fallback");
        prompt.Should().Contain("prompt is too long");
        prompt.Should().Contain("工具输出必须分批读取或摘要化处理");
    }

    /// <summary>
    /// LLM 读取超时应生成小步恢复提示，避免原样重复大请求。
    /// </summary>
    [Fact]
    public void TryBuild_ShouldBuildStepDownPrompt_WhenReadTimeoutDetected()
    {
        var records = new[]
        {
            CreateRecord(
                ToolFailureReason.TransientNetworkFailure,
                userMessage: "read timeout reached")
        };

        var prompt = ContextOverflowRepairPromptBuilder.TryBuild("生成大型方案", records);

        prompt.Should().NotBeNull();
        prompt.Should().Contain("## LLM 超时恢复指令");
        prompt.Should().Contain("生成大型方案");
        prompt.Should().Contain("failureReason=TransientNetworkFailure");
        prompt.Should().Contain("suggestedAction=Fallback");
        prompt.Should().Contain("read timeout reached");
        prompt.Should().Contain("拆成可验证的小步");
        prompt.Should().Contain("不要原样重复上一次超时请求");
    }

    /// <summary>
    /// 恢复提示自身也应压缩长目标和长失败摘要，避免二次撑爆上下文。
    /// </summary>
    [Fact]
    public void TryBuild_ShouldCompressLongGoalAndFailureSummary()
    {
        var longGoal = new string('目', 1400);
        var longError = new string('错', 1200);
        var records = new[]
        {
            CreateRecord(
                ToolFailureReason.ContextOverflow,
                userMessage: longError)
        };

        var prompt = ContextOverflowRepairPromptBuilder.TryBuild(longGoal, records);

        prompt.Should().NotBeNull();
        prompt.Should().Contain("## 上下文溢出恢复指令");
        prompt.Should().Contain("Total output chars: 1400");
        prompt.Should().Contain("Total output chars: 1200");
        prompt.Should().Contain("已按模型可见预算省略中间内容");
        prompt.Should().NotContain(longGoal);
        prompt.Should().NotContain(longError);
    }

    /// <summary>
    /// 多条失败摘要应按完整结构去重并限制条数，避免重复错误淹没关键来源。
    /// </summary>
    [Fact]
    public void TryBuild_ShouldDeduplicateAndLimitStructuredFailureSummary()
    {
        var records = new[]
        {
            CreateRecord(
                ToolFailureReason.ContextOverflow,
                userMessage: "same overflow",
                toolName: "HostService.ExecuteCommand"),
            CreateRecord(
                ToolFailureReason.ContextOverflow,
                userMessage: "same overflow",
                toolName: "HostService.ExecuteCommand"),
            CreateRecord(
                ToolFailureReason.ContextOverflow,
                userMessage: "second overflow",
                toolName: "CodeSearch.Search"),
            CreateRecord(
                ToolFailureReason.ContextOverflow,
                userMessage: "third overflow",
                toolName: "OpenViking.Search")
        };

        var prompt = ContextOverflowRepairPromptBuilder.TryBuild("分析失败来源", records);

        prompt.Should().NotBeNull();
        prompt.Should().Contain("HostService.ExecuteCommand: failureReason=ContextOverflow");
        prompt.Should().Contain("CodeSearch.Search: failureReason=ContextOverflow");
        prompt.Should().NotContain("OpenViking.Search");
        prompt.Should().Contain("same overflow").And.Contain("second overflow");
        prompt.Should().Contain("same overflow", Exactly.Once());
    }

    /// <summary>
    /// 普通连接失败不应触发小步恢复提示。
    /// </summary>
    [Fact]
    public void TryBuild_ShouldReturnNull_WhenNetworkFailureIsNotReadTimeout()
    {
        var records = new[]
        {
            CreateRecord(
                ToolFailureReason.TransientNetworkFailure,
                userMessage: "connection reset by peer")
        };

        var prompt = ContextOverflowRepairPromptBuilder.TryBuild("调用模型", records);

        prompt.Should().BeNull();
    }

    private static ToolExecutionRecord CreateRecord(
        ToolFailureReason failureReason,
        string? userMessage = null,
        string toolName = "HostService.ExecuteCommand",
        ToolSuggestedAction suggestedAction = ToolSuggestedAction.Fallback)
    {
        return new ToolExecutionRecord
        {
            ToolCallId = Guid.NewGuid(),
            ToolName = toolName,
            Arguments = "{\"command\":\"dotnet build\"}",
            Success = false,
            FailureReason = failureReason,
            UserMessage = userMessage,
            SuggestedAction = suggestedAction
        };
    }
}
