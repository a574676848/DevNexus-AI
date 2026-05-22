using DevNexus.Core.Models.Evaluation;
using DevNexus.Core.Services.Chat;
using DevNexus.Shared.Constants;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 工具调用截断恢复提示构建器测试。
/// </summary>
public sealed class ToolCallTruncationRepairPromptBuilderTests
{
    /// <summary>
    /// 仅截断参数验证失败应匹配截断恢复分支。
    /// </summary>
    [Fact]
    public void IsTruncation_ShouldMatchOnlyTruncatedArgumentsMessage()
    {
        ToolCallTruncationRepairPromptBuilder
            .IsTruncation(AiOptimizationConstants.ToolValidationMessages.TruncatedArguments)
            .Should()
            .BeTrue();

        ToolCallTruncationRepairPromptBuilder
            .IsTruncation(AiOptimizationConstants.ToolValidationMessages.InvalidJson)
            .Should()
            .BeFalse();
    }

    /// <summary>
    /// 恢复提示应要求缩小步骤并重新提供完整 JSON 参数。
    /// </summary>
    [Fact]
    public void Build_ShouldIncludeSmallerStepGuidanceAndToolNames()
    {
        var records = new[]
        {
            CreateFailedRecord("HostService.ExecuteCommand"),
            CreateFailedRecord("HostService.ExecuteCommand"),
            CreateFailedRecord("CodeExecution.Run")
        };

        var prompt = ToolCallTruncationRepairPromptBuilder.Build(records);

        prompt.Should().Contain("受影响工具: HostService.ExecuteCommand, CodeExecution.Run");
        prompt.Should().Contain("不要原样重试同一个大工具调用");
        prompt.Should().Contain("将任务拆成更小步骤");
        prompt.Should().Contain("必须提供完整 JSON 参数");
    }

    private static ToolExecutionRecord CreateFailedRecord(string toolName)
    {
        return new ToolExecutionRecord
        {
            ToolCallId = Guid.NewGuid(),
            ToolName = toolName,
            Arguments = "{}",
            Success = false
        };
    }
}
