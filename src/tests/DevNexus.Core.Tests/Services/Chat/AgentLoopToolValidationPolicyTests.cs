using DevNexus.Core.Models.Evaluation;
using DevNexus.Core.Services.Chat;
using DevNexus.Shared.Constants;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// Agent Loop 工具调用验证策略测试。
/// </summary>
public sealed class AgentLoopToolValidationPolicyTests
{
    /// <summary>
    /// 有效工具调用允许继续质量评估。
    /// </summary>
    [Fact]
    public void Decide_ShouldContinue_WhenToolRecordsAreValid()
    {
        var decision = AgentLoopToolValidationPolicy.Decide(
            "检索资料",
            [CreateRecord()]);

        decision.CanContinue.Should().BeTrue();
        decision.NeedsRetry.Should().BeFalse();
    }

    /// <summary>
    /// 截断类工具调用应直接生成确定性小步重试提示。
    /// </summary>
    [Fact]
    public void Decide_ShouldRetry_WhenToolArgumentsLookTruncated()
    {
        var decision = AgentLoopToolValidationPolicy.Decide(
            "读取文件",
            [CreateRecord(arguments: "{}", success: false, failureReason: ToolFailureReason.ToolFormatError)]);

        decision.CanContinue.Should().BeFalse();
        decision.NeedsRetry.Should().BeTrue();
        decision.RepairPrompt.Should().Contain("工具调用参数生成过程中被截断");
    }

    /// <summary>
    /// 非截断协议错误应停止自动修复，避免污染后续评估。
    /// </summary>
    [Fact]
    public void Decide_ShouldStop_WhenToolCallIdMissing()
    {
        var decision = AgentLoopToolValidationPolicy.Decide(
            "执行命令",
            [CreateRecord(toolCallId: Guid.Empty)]);

        decision.CanContinue.Should().BeFalse();
        decision.NeedsRetry.Should().BeFalse();
        decision.StopMessage.Should().Be(AiOptimizationConstants.ToolValidationMessages.MissingToolCallId);
    }

    private static ToolExecutionRecord CreateRecord(
        Guid? toolCallId = null,
        string arguments = """{"query":"DevNexus"}""",
        bool success = true,
        ToolFailureReason failureReason = ToolFailureReason.None)
    {
        return new ToolExecutionRecord
        {
            ToolCallId = toolCallId ?? Guid.NewGuid(),
            ToolName = "WebSearch.SearchAsync",
            Arguments = arguments,
            Success = success,
            FailureReason = failureReason
        };
    }
}
