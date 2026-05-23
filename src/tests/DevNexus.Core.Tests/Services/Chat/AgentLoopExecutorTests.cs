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
    /// 多工具混合失败中，终端运行态续接不应被非终端补参动作抢占。
    /// </summary>
    [Fact]
    public async Task EvaluateAndBuildRepairAsync_ShouldPreferRuntimeContinuation_WhenMixedToolsContainInputRequest()
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
            "运行测试并整理结果",
            "一个工具缺少说明，另一个终端命令仍在运行",
            [
                new ToolExecutionRecord
                {
                    ToolCallId = Guid.NewGuid(),
                    ToolName = "TaskService.CreateAsync",
                    Arguments = """{"title":"运行结果整理","description":"待模型补充"}""",
                    Success = false,
                    RequiresHumanIntervention = false,
                    SuggestedAction = ToolSuggestedAction.PromptUserInput,
                    RequestedUserInputLabel = "任务说明",
                    ErrorSummary = "缺少任务说明"
                },
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
        repairPrompt.Should().Contain("首要动作: WaitForCompletion");
        repairPrompt.Should().Contain("HostService.WaitCommandAsync");
        repairPrompt.Should().Contain("不要调用 HostService.ExecuteCommandAsync 重新启动相同命令");
        repairBuilder.LastContext.Should().BeNull();
        ruleEvaluator.CallCount.Should().Be(0);
        llmEvaluator.CallCount.Should().Be(0);
    }

    /// <summary>
    /// 多工具混合失败中，停止终端命令应优先于等待完成和普通补参。
    /// </summary>
    [Fact]
    public async Task EvaluateAndBuildRepairAsync_ShouldPreferStopCommand_WhenMixedRuntimeActionsExist()
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
            "停止卡住的测试命令并整理原因",
            "停止请求未完成，另一个工具仍在等待结果",
            [
                new ToolExecutionRecord
                {
                    ToolCallId = Guid.NewGuid(),
                    ToolName = "TaskService.CreateAsync",
                    Arguments = """{"title":"停止结果整理","description":"待补充"}""",
                    Success = false,
                    SuggestedAction = ToolSuggestedAction.PromptUserInput,
                    RequestedUserInputLabel = "任务说明",
                    ErrorSummary = "缺少任务说明"
                },
                new ToolExecutionRecord
                {
                    ToolCallId = Guid.NewGuid(),
                    ToolName = "HostService.ExecuteCommandAsync",
                    Arguments = """{"command":"dotnet test"}""",
                    Success = false,
                    Retryable = true,
                    SuggestedAction = ToolSuggestedAction.WaitForCompletion,
                    ErrorSummary = "命令仍在运行"
                },
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
        repairPrompt.Should().Contain("首要动作: StopCommand");
        repairPrompt.Should().Contain("HostService.StopCommandAsync");
        repairPrompt.Should().Contain("不要调用 HostService.ExecuteCommandAsync 重新启动相同命令");
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

    /// <summary>
    /// 长目标多轮恢复应先处理确定性恢复，普通失败才进入当前 Provider 的 LLM 评估。
    /// </summary>
    [Fact]
    public async Task EvaluateAndBuildRepairAsync_ShouldKeepDeterministicRecoveryAcrossProvidersAndAttempts()
    {
        var ruleEvaluator = new CountingEvaluator();
        var llmEvaluator = new CountingEvaluator();
        var repairBuilder = new StubRepairContextBuilder();
        var executor = new AgentLoopExecutor(
            ruleEvaluator,
            llmEvaluator,
            repairBuilder,
            NullLogger<AgentLoopExecutor>.Instance);
        var firstProviderId = Guid.NewGuid();
        var secondProviderId = Guid.NewGuid();
        var userGoal = string.Join(
            "\n",
            "运行完整验证并整理结果。",
            "需要覆盖 build、定向测试、日志证据和失败恢复说明。",
            "如果工具输出过长，必须分批读取并保留下一步动作。");

        var first = await executor.EvaluateAndBuildRepairAsync(
            userGoal,
            "Provider 流式响应读取超时",
            [
                new ToolExecutionRecord
                {
                    ToolCallId = Guid.NewGuid(),
                    ToolName = "LLM.StreamChatCompletionAsync",
                    Arguments = """{"requestId":"stream-timeout"}""",
                    Success = false,
                    FailureReason = ToolFailureReason.TransientNetworkFailure,
                    UserMessage = "read timeout while streaming response",
                    SuggestedAction = ToolSuggestedAction.Fallback
                }
            ],
            attempt: 0,
            providerId: firstProviderId,
            CancellationToken.None);

        first.needsRepair.Should().BeTrue();
        first.repairPrompt.Should().Contain("## LLM 超时恢复指令");
        first.repairPrompt.Should().Contain("拆成可验证的小步");
        ruleEvaluator.CallCount.Should().Be(0);
        llmEvaluator.CallCount.Should().Be(0);

        var second = await executor.EvaluateAndBuildRepairAsync(
            userGoal,
            "验证命令仍在运行",
            [
                new ToolExecutionRecord
                {
                    ToolCallId = Guid.NewGuid(),
                    ToolName = "HostService.ExecuteCommandAsync",
                    Arguments = """{"command":"dotnet test src/DevNexus.sln --no-build"}""",
                    Success = false,
                    Retryable = true,
                    SuggestedAction = ToolSuggestedAction.WaitForCompletion,
                    ErrorSummary = "recommendedTool: HostService.WaitCommandAsync"
                }
            ],
            attempt: 1,
            providerId: secondProviderId,
            CancellationToken.None);

        second.needsRepair.Should().BeTrue();
        second.repairPrompt.Should().Contain("## 运行态续接指令");
        second.repairPrompt.Should().Contain("primaryAction: WaitForCompletion");
        second.repairPrompt.Should().Contain("HostService.WaitCommandAsync");
        ruleEvaluator.CallCount.Should().Be(0);
        llmEvaluator.CallCount.Should().Be(0);

        var third = await executor.EvaluateAndBuildRepairAsync(
            userGoal,
            "普通工具失败，需要模型判断是否可修复",
            [
                new ToolExecutionRecord
                {
                    ToolCallId = Guid.NewGuid(),
                    ToolName = "KnowledgeBase.SearchAsync",
                    Arguments = """{"query":""}""",
                    Success = false,
                    Retryable = false,
                    FailureReason = ToolFailureReason.FatalExecutionError,
                    ErrorSummary = "检索参数为空",
                    SuggestedAction = ToolSuggestedAction.Abort
                }
            ],
            attempt: 2,
            providerId: secondProviderId,
            CancellationToken.None);

        third.needsRepair.Should().BeFalse();
        third.repairPrompt.Should().BeNull();
        ruleEvaluator.CallCount.Should().Be(0);
        llmEvaluator.CallCount.Should().Be(1);
        llmEvaluator.LastContext.Should().NotBeNull();
        llmEvaluator.LastContext!.ProviderId.Should().Be(secondProviderId);
        llmEvaluator.LastContext.Attempt.Should().Be(2);
        repairBuilder.LastContext.Should().BeNull();
    }

    private sealed class CountingEvaluator : IRuleResponseEvaluator, ILlmResponseEvaluator
    {
        public int CallCount { get; private set; }

        public EvaluationContext? LastContext { get; private set; }

        public Task<EvaluationResult> EvaluateAsync(
            EvaluationContext context,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastContext = context;
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
