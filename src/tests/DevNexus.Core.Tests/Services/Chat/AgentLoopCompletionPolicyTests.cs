using DevNexus.Core.Models.Evaluation;
using DevNexus.Core.Services.Chat;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// Agent Loop 完成判定策略测试。
/// </summary>
public sealed class AgentLoopCompletionPolicyTests
{
    /// <summary>
    /// 没有工具调用记录时应允许普通完成收尾。
    /// </summary>
    [Fact]
    public void Decide_ShouldComplete_WhenToolRecordsAreEmpty()
    {
        var decision = AgentLoopCompletionPolicy.Decide(Array.Empty<ToolExecutionRecord>());

        decision.IsComplete.Should().BeTrue();
        decision.Reason.Should().Be("tool_calls_empty");
    }

    /// <summary>
    /// 只要存在工具调用记录，即使模型文本看起来完整，也必须进入工具后处理。
    /// </summary>
    [Fact]
    public void Decide_ShouldEvaluateToolCalls_WhenToolRecordsExist()
    {
        var decision = AgentLoopCompletionPolicy.Decide(
            [
                new ToolExecutionRecord
                {
                    ToolCallId = Guid.NewGuid(),
                    ToolName = "WebSearch.SearchAsync",
                    Arguments = """{"query":"DevNexus"}""",
                    Success = true
                }
            ]);

        decision.IsComplete.Should().BeFalse();
        decision.Reason.Should().Be("tool_calls_present");
    }
}
