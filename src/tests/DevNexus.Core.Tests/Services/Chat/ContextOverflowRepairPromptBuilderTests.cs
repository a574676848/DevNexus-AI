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
        prompt.Should().Contain("read timeout reached");
        prompt.Should().Contain("拆成可验证的小步");
        prompt.Should().Contain("不要原样重复上一次超时请求");
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
        string? userMessage = null)
    {
        return new ToolExecutionRecord
        {
            ToolCallId = Guid.NewGuid(),
            ToolName = "HostService.ExecuteCommand",
            Arguments = "{\"command\":\"dotnet build\"}",
            Success = false,
            FailureReason = failureReason,
            UserMessage = userMessage,
            SuggestedAction = ToolSuggestedAction.Fallback
        };
    }
}
