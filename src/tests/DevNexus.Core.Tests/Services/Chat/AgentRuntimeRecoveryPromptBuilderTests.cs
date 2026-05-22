using DevNexus.Core.Models.Evaluation;
using DevNexus.Core.Services.Chat;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// Agent Loop 运行态恢复提示构建器测试。
/// </summary>
public sealed class AgentRuntimeRecoveryPromptBuilderTests
{
    /// <summary>
    /// 运行态恢复提示不应伪装成通用质量评估失败。
    /// </summary>
    [Fact]
    public void Build_ShouldUseRuntimeRecoveryHeader()
    {
        var prompt = AgentRuntimeRecoveryPromptBuilder.Build(
            "运行测试",
            "命令仍在运行",
            [CreateRecord(ToolSuggestedAction.WaitForCompletion)],
            ToolRecoveryStrategySummaryBuilder.Build([CreateRecord(ToolSuggestedAction.WaitForCompletion)]));

        prompt.Should().Contain("## 运行态续接指令");
        prompt.Should().Contain("直接续接同一执行上下文");
        prompt.Should().Contain("HostService.WaitCommandAsync");
        prompt.Should().NotContain("质量评估失败");
        prompt.Should().NotContain("分数");
    }

    /// <summary>
    /// 过长目标应被截断，避免运行态续接提示膨胀。
    /// </summary>
    [Fact]
    public void Build_ShouldTruncateLongUserGoal()
    {
        var longGoal = new string('目', 1100);
        var prompt = AgentRuntimeRecoveryPromptBuilder.Build(
            longGoal,
            "等待输入",
            [CreateCliInputRecord()],
            ToolRecoveryStrategySummaryBuilder.Build([CreateCliInputRecord()]));

        prompt.Should().Contain("### 原始用户目标");
        prompt.Should().Contain("... (已截断)");
        prompt.Should().NotContain(longGoal);
    }

    /// <summary>
    /// 重复运行态失败应合并展示，避免提示随循环次数线性膨胀。
    /// </summary>
    [Fact]
    public void Build_ShouldDeduplicateRepeatedRuntimeFailures()
    {
        var records = new[]
        {
            CreateRecord(ToolSuggestedAction.WaitForCompletion),
            CreateRecord(ToolSuggestedAction.WaitForCompletion),
            CreateRecord(ToolSuggestedAction.WaitForCompletion)
        };

        var prompt = AgentRuntimeRecoveryPromptBuilder.Build(
            "运行测试",
            "命令仍在运行",
            records,
            ToolRecoveryStrategySummaryBuilder.Build(records));

        prompt.Should().Contain("occurrences: 3");
        prompt.Split("- HostService.ExecuteCommandAsync:").Length.Should().Be(2);
    }

    /// <summary>
    /// CLI stdin 续接提示应避免使用产品化补参语义。
    /// </summary>
    [Fact]
    public void Build_ShouldUseCliInputStrategy_WhenCommandWaitsForStdin()
    {
        var record = CreateCliInputRecord();
        var prompt = AgentRuntimeRecoveryPromptBuilder.Build(
            "继续安装",
            "命令等待输入",
            [record],
            ToolRecoveryStrategySummaryBuilder.Build([record]));

        prompt.Should().Contain("title: 终端 stdin 续接");
        prompt.Should().Contain("向同一终端会话发送输入");
        prompt.Should().Contain("不要升级为人工挂起交互");
        prompt.Should().NotContain("请先补充必要输入");
        prompt.Should().NotContain("工具恢复需要补充输入");
    }

    /// <summary>
    /// 失败摘要过长时应压缩，避免运行态续接提示被工具输出撑大。
    /// </summary>
    [Fact]
    public void Build_ShouldCompressLongFailureSummary()
    {
        var longSummary = new string('错', 500);
        var record = CreateRecord(ToolSuggestedAction.WaitForCompletion) with
        {
            ErrorSummary = longSummary
        };

        var prompt = AgentRuntimeRecoveryPromptBuilder.Build(
            "运行测试",
            "命令仍在运行",
            [record],
            ToolRecoveryStrategySummaryBuilder.Build([record]));

        prompt.Should().Contain("Total output chars: 500");
        prompt.Should().Contain("已按模型可见预算省略中间内容");
        prompt.Should().NotContain(longSummary);
    }

    /// <summary>
    /// 策略说明过长时也应压缩，避免失败摘要从策略段绕过预算。
    /// </summary>
    [Fact]
    public void Build_ShouldCompressLongStrategyMessage()
    {
        var longMessage = new string('策', 800);
        var record = CreateRecord(ToolSuggestedAction.WaitForCompletion);
        var summary = ToolRecoveryStrategySummaryBuilder.Build([record]) with
        {
            Message = longMessage
        };

        var prompt = AgentRuntimeRecoveryPromptBuilder.Build(
            "运行测试",
            "命令仍在运行",
            [record],
            summary);

        prompt.Should().Contain("Total output chars: 800");
        prompt.Should().Contain("已按模型可见预算省略中间内容");
        prompt.Should().NotContain(longMessage);
    }

    /// <summary>
    /// 上一轮输出过长时应复用工具输出预算压缩器。
    /// </summary>
    [Fact]
    public void Build_ShouldCompressLongPreviousOutput()
    {
        var longOutput = new string('出', 1500);
        var record = CreateRecord(ToolSuggestedAction.WaitForCompletion);

        var prompt = AgentRuntimeRecoveryPromptBuilder.Build(
            "运行测试",
            longOutput,
            [record],
            ToolRecoveryStrategySummaryBuilder.Build([record]));

        prompt.Should().Contain("Total output chars: 1500");
        prompt.Should().Contain("已按模型可见预算省略中间内容");
        prompt.Should().NotContain(longOutput);
        prompt.Should().NotContain("... (已截断)");
    }

    /// <summary>
    /// WaitForCompletion 应明确禁止重新调用执行命令工具。
    /// </summary>
    [Fact]
    public void Build_ShouldForbidExecuteCommand_WhenWaitingForCompletion()
    {
        var record = CreateRecord(ToolSuggestedAction.WaitForCompletion);

        var prompt = AgentRuntimeRecoveryPromptBuilder.Build(
            "运行测试",
            "命令仍在运行",
            [record],
            ToolRecoveryStrategySummaryBuilder.Build([record]));

        prompt.Should().Contain("HostService.WaitCommandAsync");
        prompt.Should().Contain("不要调用 HostService.ExecuteCommandAsync 重新启动相同命令");
    }

    /// <summary>
    /// StopCommand 应明确禁止重新调用执行命令工具。
    /// </summary>
    [Fact]
    public void Build_ShouldForbidExecuteCommand_WhenStoppingCommand()
    {
        var record = CreateRecord(ToolSuggestedAction.StopCommand);

        var prompt = AgentRuntimeRecoveryPromptBuilder.Build(
            "停止命令",
            "停止请求未完成",
            [record],
            ToolRecoveryStrategySummaryBuilder.Build([record]));

        prompt.Should().Contain("HostService.StopCommandAsync");
        prompt.Should().Contain("不要调用 HostService.ExecuteCommandAsync 重新启动相同命令");
    }

    /// <summary>
    /// CLI stdin 续接应明确禁止重新调用执行命令工具。
    /// </summary>
    [Fact]
    public void Build_ShouldForbidExecuteCommand_WhenSendingCliInput()
    {
        var record = CreateCliInputRecord();

        var prompt = AgentRuntimeRecoveryPromptBuilder.Build(
            "继续安装",
            "命令等待输入",
            [record],
            ToolRecoveryStrategySummaryBuilder.Build([record]));

        prompt.Should().Contain("HostService.SendCommandInputAsync");
        prompt.Should().Contain("不要调用 HostService.ExecuteCommandAsync 重新启动相同命令");
    }

    private static ToolExecutionRecord CreateRecord(ToolSuggestedAction action)
    {
        return new ToolExecutionRecord
        {
            ToolCallId = Guid.NewGuid(),
            ToolName = "HostService.ExecuteCommandAsync",
            Arguments = """{"command":"dotnet test"}""",
            Success = false,
            Retryable = true,
            SuggestedAction = action,
            ErrorSummary = "命令仍在运行"
        };
    }

    private static ToolExecutionRecord CreateCliInputRecord()
    {
        return new ToolExecutionRecord
        {
            ToolCallId = Guid.NewGuid(),
            ToolName = "HostService.WaitCommandAsync",
            Arguments = """{"sessionId":"current"}""",
            Success = false,
            SuggestedAction = ToolSuggestedAction.PromptUserInput,
            RequestedUserInputLabel = "终端输入",
            ErrorSummary = "recommendedTool: HostService.SendCommandInputAsync"
        };
    }
}
