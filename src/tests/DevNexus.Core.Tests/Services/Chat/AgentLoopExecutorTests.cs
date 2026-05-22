using DevNexus.Core.Abstractions;
using DevNexus.Core.Models.Evaluation;
using DevNexus.Core.Services.Chat;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// Agent Loop 执行器测试。
/// </summary>
public sealed class AgentLoopExecutorTests
{
    /// <summary>
    /// 截断类工具调用应在进入评估器前直接生成确定性修复提示。
    /// </summary>
    [Fact]
    public async Task EvaluateAndBuildRepairAsync_ShouldBypassEvaluators_WhenToolArgumentsTruncated()
    {
        var ruleEvaluator = new CountingEvaluator();
        var llmEvaluator = new CountingEvaluator();
        var executor = new AgentLoopExecutor(
            ruleEvaluator,
            llmEvaluator,
            new StubRepairContextBuilder(),
            NullLogger<AgentLoopExecutor>.Instance);

        var (needsRepair, repairPrompt) = await executor.EvaluateAndBuildRepairAsync(
            "读取文件",
            "工具调用失败",
            [
                new ToolExecutionRecord
                {
                    ToolCallId = Guid.NewGuid(),
                    ToolName = "HostService.ReadFile",
                    Arguments = "{}",
                    Success = false,
                    FailureReason = ToolFailureReason.ToolFormatError
                }
            ],
            attempt: 0,
            providerId: Guid.NewGuid(),
            CancellationToken.None);

        needsRepair.Should().BeTrue();
        repairPrompt.Should().Contain("工具调用参数生成过程中被截断");
        ruleEvaluator.CallCount.Should().Be(0);
        llmEvaluator.CallCount.Should().Be(0);
    }

    /// <summary>
    /// 终端命令仍在运行时应直接生成续接提示，不进入通用评估器。
    /// </summary>
    [Fact]
    public async Task EvaluateAndBuildRepairAsync_ShouldBypassEvaluators_WhenCommandStillRunning()
    {
        var ruleEvaluator = new CountingEvaluator();
        var llmEvaluator = new CountingEvaluator();
        var repairBuilder = new StubRepairContextBuilder();
        var executor = new AgentLoopExecutor(
            ruleEvaluator,
            llmEvaluator,
            repairBuilder,
            NullLogger<AgentLoopExecutor>.Instance);

        var (needsRepair, repairPrompt) = await executor.EvaluateAndBuildRepairAsync(
            "运行测试",
            "命令仍在运行",
            [
                new ToolExecutionRecord
                {
                    ToolCallId = Guid.NewGuid(),
                    ToolName = "HostService.ExecuteCommandAsync",
                    Arguments = """{"command":"dotnet test"}""",
                    Success = false,
                    Retryable = true,
                    SuggestedAction = ToolSuggestedAction.WaitForCompletion,
                    ErrorSummary = "命令仍在运行"
                }
            ],
            attempt: 0,
            providerId: Guid.NewGuid(),
            CancellationToken.None);

        needsRepair.Should().BeTrue();
        repairPrompt.Should().Contain("## 运行态续接指令");
        repairPrompt.Should().Contain("primaryAction: WaitForCompletion");
        repairPrompt.Should().Contain("HostService.WaitCommandAsync");
        repairPrompt.Should().NotContain("质量评估");
        repairBuilder.LastContext.Should().BeNull();
        ruleEvaluator.CallCount.Should().Be(0);
        llmEvaluator.CallCount.Should().Be(0);
    }

    /// <summary>
    /// 终端停止未闭环时应直接生成停止续接提示，不进入通用评估器。
    /// </summary>
    [Fact]
    public async Task EvaluateAndBuildRepairAsync_ShouldBypassEvaluators_WhenStopCommandIsRequired()
    {
        var ruleEvaluator = new CountingEvaluator();
        var llmEvaluator = new CountingEvaluator();
        var repairBuilder = new StubRepairContextBuilder();
        var executor = new AgentLoopExecutor(
            ruleEvaluator,
            llmEvaluator,
            repairBuilder,
            NullLogger<AgentLoopExecutor>.Instance);

        var (needsRepair, repairPrompt) = await executor.EvaluateAndBuildRepairAsync(
            "停止命令",
            "停止请求未完成",
            [
                new ToolExecutionRecord
                {
                    ToolCallId = Guid.NewGuid(),
                    ToolName = "HostService.StopCommandAsync",
                    Arguments = """{"sessionId":"current"}""",
                    Success = false,
                    Retryable = true,
                    SuggestedAction = ToolSuggestedAction.StopCommand,
                    ErrorSummary = "recommendedTool: HostService.StopCommandAsync"
                }
            ],
            attempt: 0,
            providerId: Guid.NewGuid(),
            CancellationToken.None);

        needsRepair.Should().BeTrue();
        repairPrompt.Should().Contain("## 运行态续接指令");
        repairPrompt.Should().Contain("primaryAction: StopCommand");
        repairPrompt.Should().Contain("HostService.StopCommandAsync");
        repairPrompt.Should().NotContain("质量评估");
        repairBuilder.LastContext.Should().BeNull();
        ruleEvaluator.CallCount.Should().Be(0);
        llmEvaluator.CallCount.Should().Be(0);
    }

    /// <summary>
    /// CLI stdin 续接应直接生成同会话输入提示，不进入通用评估器。
    /// </summary>
    [Fact]
    public async Task EvaluateAndBuildRepairAsync_ShouldBypassEvaluators_WhenCliInputCanContinue()
    {
        var ruleEvaluator = new CountingEvaluator();
        var llmEvaluator = new CountingEvaluator();
        var repairBuilder = new StubRepairContextBuilder();
        var executor = new AgentLoopExecutor(
            ruleEvaluator,
            llmEvaluator,
            repairBuilder,
            NullLogger<AgentLoopExecutor>.Instance);

        var (needsRepair, repairPrompt) = await executor.EvaluateAndBuildRepairAsync(
            "继续安装",
            "命令等待输入",
            [
                new ToolExecutionRecord
                {
                    ToolCallId = Guid.NewGuid(),
                    ToolName = "HostService.WaitCommandAsync",
                    Arguments = """{"sessionId":"current"}""",
                    Success = false,
                    RequiresHumanIntervention = false,
                    SuggestedAction = ToolSuggestedAction.PromptUserInput,
                    RequestedUserInputLabel = "终端输入",
                    ErrorSummary = "recommendedTool: HostService.SendCommandInputAsync"
                }
            ],
            attempt: 0,
            providerId: Guid.NewGuid(),
            CancellationToken.None);

        needsRepair.Should().BeTrue();
        repairPrompt.Should().Contain("## 运行态续接指令");
        repairPrompt.Should().Contain("primaryAction: PromptUserInput");
        repairPrompt.Should().Contain("HostService.SendCommandInputAsync");
        repairPrompt.Should().Contain("不要升级为人工挂起交互");
        repairPrompt.Should().NotContain("质量评估");
        repairBuilder.LastContext.Should().BeNull();
        ruleEvaluator.CallCount.Should().Be(0);
        llmEvaluator.CallCount.Should().Be(0);
    }

    /// <summary>
    /// 非终端补参不应被误判为 CLI stdin 续接，仍交给后续评估链处理。
    /// </summary>
    [Fact]
    public async Task EvaluateAndBuildRepairAsync_ShouldUseEvaluator_WhenInputRequestIsNotCliContinuation()
    {
        var ruleEvaluator = new CountingEvaluator();
        var llmEvaluator = new CountingEvaluator();
        var executor = new AgentLoopExecutor(
            ruleEvaluator,
            llmEvaluator,
            new StubRepairContextBuilder(),
            NullLogger<AgentLoopExecutor>.Instance);

        var (needsRepair, repairPrompt) = await executor.EvaluateAndBuildRepairAsync(
            "创建任务",
            "缺少参数",
            [
                new ToolExecutionRecord
                {
                    ToolCallId = Guid.NewGuid(),
                    ToolName = "TaskService.CreateAsync",
                    Arguments = """{"title":""}""",
                    Success = false,
                    Retryable = false,
                    RequiresHumanIntervention = false,
                    SuggestedAction = ToolSuggestedAction.PromptUserInput,
                    RequestedUserInputLabel = "任务标题",
                    ErrorSummary = "缺少任务标题"
                }
            ],
            attempt: 0,
            providerId: Guid.NewGuid(),
            CancellationToken.None);

        needsRepair.Should().BeFalse();
        repairPrompt.Should().BeNull();
        ruleEvaluator.CallCount.Should().Be(0);
        llmEvaluator.CallCount.Should().Be(1);
    }

    private sealed class CountingEvaluator : IRuleResponseEvaluator, ILlmResponseEvaluator
    {
        public int CallCount { get; private set; }

        public Task<EvaluationResult> EvaluateAsync(
            EvaluationContext context,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new EvaluationResult { Passed = true });
        }
    }

    private sealed class StubRepairContextBuilder : IRepairContextBuilder
    {
        public EvaluationContext? LastContext { get; private set; }

        public string Build(EvaluationContext context, EvaluationResult evaluation)
        {
            LastContext = context;
            return "repair";
        }
    }
}
