using DevNexus.Core.Models.Evaluation;
using DevNexus.Core.Services.Chat;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// Agent Loop 修复提示构建器测试。
/// </summary>
public sealed class AgentRepairPromptBuilderTests
{
    /// <summary>
    /// 修复提示应包含评估反馈、分数、建议和停止策略。
    /// </summary>
    [Fact]
    public void Build_ShouldIncludeEvaluationSections()
    {
        var builder = new AgentRepairPromptBuilder();
        var context = new EvaluationContext
        {
            Goal = "实现登录流程并补充验证",
            Result = "上一次输出",
            Attempt = 2
        };
        var evaluation = CreateEvaluation();

        var prompt = builder.Build(context, evaluation);

        prompt.Should().Contain("## 修复指令 (第 2 次重试)");
        prompt.Should().Contain("### 原始用户目标");
        prompt.Should().Contain("实现登录流程并补充验证");
        prompt.Should().Contain("### 评估反馈");
        prompt.Should().Contain("缺少关键步骤");
        prompt.Should().Contain("### 各维度分数");
        prompt.Should().Contain("1. 补充验证证据");
        prompt.Should().Contain("[AGENT_LOOP_STOP]");
    }

    /// <summary>
    /// 修复提示应纳入失败工具的结构化恢复字段。
    /// </summary>
    [Fact]
    public void Build_ShouldIncludeFailedToolRecoveryFields()
    {
        var builder = new AgentRepairPromptBuilder();
        var context = new EvaluationContext
        {
            Result = "工具失败",
            ToolRecords =
            [
                new ToolExecutionRecord
                {
                    ToolName = "HostService.ExecuteCommand",
                    Success = false,
                    FailureReason = ToolFailureReason.MissingUserInput,
                    Retryable = false,
                    RequiresHumanIntervention = true,
                    ShouldFallback = false,
                    ShouldRotateCredential = false,
                    SuggestedAction = ToolSuggestedAction.PromptUserInput,
                    RequestedUserInputKind = "text",
                    RequestedUserInputLabel = "必要参数",
                    UserMessage = "请补充参数",
                    ErrorSummary = "参数为空"
                }
            ]
        };

        var prompt = builder.Build(context, CreateEvaluation());

        prompt.Should().Contain("### 失败的工具调用记录");
        prompt.Should().Contain("failureReason: MissingUserInput");
        prompt.Should().Contain("suggestedAction: PromptUserInput");
        prompt.Should().Contain("requestedUserInputLabel: 必要参数");
        prompt.Should().Contain("error: 参数为空");
        prompt.Should().Contain("### 工具恢复策略");
        prompt.Should().Contain("primaryAction: PromptUserInput");
        prompt.Should().Contain("failureReasons: MissingUserInput");
        prompt.Should().Contain("不要把它当作普通重试");
    }

    /// <summary>
    /// 审批类失败应在修复提示中优先声明人工前置条件。
    /// </summary>
    [Fact]
    public void Build_ShouldWarnApprovalFailureIsNotNormalRetry()
    {
        var builder = new AgentRepairPromptBuilder();
        var context = new EvaluationContext
        {
            Result = "审批失败",
            ToolRecords =
            [
                new ToolExecutionRecord
                {
                    ToolName = "HostService.ExecuteCommand",
                    Success = false,
                    FailureReason = ToolFailureReason.ApprovalRequired,
                    RequiresHumanIntervention = true,
                    SuggestedAction = ToolSuggestedAction.RequestApproval,
                    ErrorSummary = "需要审批"
                },
                new ToolExecutionRecord
                {
                    ToolName = "Knowledge.Search",
                    Success = false,
                    FailureReason = ToolFailureReason.TransientNetworkFailure,
                    Retryable = true,
                    SuggestedAction = ToolSuggestedAction.Retry,
                    ErrorSummary = "网络抖动"
                }
            ]
        };

        var prompt = builder.Build(context, CreateEvaluation());

        prompt.Should().Contain("primaryAction: RequestApproval");
        prompt.Should().Contain("orderedActions: RequestApproval, Retry");
        prompt.Should().Contain("failureReasons: ApprovalRequired, TransientNetworkFailure");
        prompt.Should().Contain("不要把它当作普通重试");
    }

    /// <summary>
    /// 重复的同类工具失败应合并展示，并保留出现次数。
    /// </summary>
    [Fact]
    public void Build_ShouldDeduplicateRepeatedToolFailures()
    {
        var builder = new AgentRepairPromptBuilder();
        var context = new EvaluationContext
        {
            Result = "工具重复失败",
            ToolRecords =
            [
                CreateRepeatedFailure(),
                CreateRepeatedFailure(),
                CreateRepeatedFailure()
            ]
        };

        var prompt = builder.Build(context, CreateEvaluation());

        prompt.Should().Contain("occurrences: 3");
        prompt.Should().Contain("error: 命令仍在运行");
        prompt.Should().Contain("primaryAction: WaitForCompletion");
        prompt.Split("- **HostService.WaitCommandAsync**").Length.Should().Be(2);
    }

    /// <summary>
    /// 缺少 ErrorSummary 时应保留底层错误正文，并按同一正文区分失败。
    /// </summary>
    [Fact]
    public void Build_ShouldUseErrorMessageAndDistinguishFailures_WhenSummaryIsMissing()
    {
        var builder = new AgentRepairPromptBuilder();
        var context = new EvaluationContext
        {
            Result = "工具失败",
            ToolRecords =
            [
                CreateRepeatedFailure() with
                {
                    ErrorSummary = null,
                    ErrorMessage = "first low level failure"
                },
                CreateRepeatedFailure() with
                {
                    ErrorSummary = null,
                    ErrorMessage = "second low level failure"
                }
            ]
        };

        var prompt = builder.Build(context, CreateEvaluation());

        prompt.Should().Contain("error: first low level failure");
        prompt.Should().Contain("error: second low level failure");
        prompt.Should().NotContain("occurrences: 2");
    }

    /// <summary>
    /// 工具错误正文过长时应压缩，避免通用修复提示被底层错误撑大。
    /// </summary>
    [Fact]
    public void Build_ShouldCompressLongToolError()
    {
        var longError = new string('错', 900);
        var builder = new AgentRepairPromptBuilder();
        var context = new EvaluationContext
        {
            Result = "工具失败",
            ToolRecords =
            [
                CreateRepeatedFailure() with
                {
                    ErrorSummary = longError
                }
            ]
        };

        var prompt = builder.Build(context, CreateEvaluation());

        prompt.Should().Contain("Total output chars: 900");
        prompt.Should().Contain("已按模型可见预算省略中间内容");
        prompt.Should().NotContain(longError);
    }

    /// <summary>
    /// 仍在运行的终端命令应明确引导模型调用续接工具，而不是重启命令。
    /// </summary>
    [Fact]
    public void Build_ShouldGuideCliContinuation_WhenCommandStillRunning()
    {
        var builder = new AgentRepairPromptBuilder();
        var context = new EvaluationContext
        {
            Result = "命令仍在运行",
            ToolRecords =
            [
                new ToolExecutionRecord
                {
                    ToolName = "HostService.ExecuteCommand",
                    Success = false,
                    FailureReason = ToolFailureReason.None,
                    Retryable = true,
                    SuggestedAction = ToolSuggestedAction.WaitForCompletion,
                    ErrorSummary = "命令仍在运行"
                }
            ]
        };

        var prompt = builder.Build(context, CreateEvaluation());

        prompt.Should().Contain("primaryAction: WaitForCompletion");
        prompt.Should().Contain("HostService.WaitCommandAsync");
        prompt.Should().Contain("HostService.SendCommandInputAsync");
        prompt.Should().Contain("不要重新启动相同命令");
    }

    /// <summary>
    /// 终端等待 stdin 时应引导模型发送输入，而不是升级成人工挂起交互。
    /// </summary>
    [Fact]
    public void Build_ShouldGuideCliInputContinuation_WhenCommandWaitsForInput()
    {
        var builder = new AgentRepairPromptBuilder();
        var context = new EvaluationContext
        {
            Result = "命令等待输入",
            ToolRecords =
            [
                new ToolExecutionRecord
                {
                    ToolName = "HostService.WaitCommandAsync",
                    Success = false,
                    FailureReason = ToolFailureReason.MissingUserInput,
                    Retryable = false,
                    RequiresHumanIntervention = false,
                    SuggestedAction = ToolSuggestedAction.PromptUserInput,
                    RequestedUserInputKind = "stdin",
                    RequestedUserInputLabel = "终端输入",
                    UserMessage = "终端正在等待输入，应通过 HostService.SendCommandInputAsync 向同一会话发送 stdin。",
                    ErrorSummary = "waitingForInput: true"
                }
            ]
        };

        var prompt = builder.Build(context, CreateEvaluation());

        prompt.Should().Contain("primaryAction: PromptUserInput");
        prompt.Should().Contain("HostService.SendCommandInputAsync");
        prompt.Should().Contain("HostService.WaitCommandAsync");
        prompt.Should().Contain("不要重新启动相同命令");
        prompt.Should().Contain("不要把它升级为人工挂起交互");
        prompt.Should().NotContain("当前首要动作存在人工前置条件");
    }

    /// <summary>
    /// 停止命令未闭环时应引导模型继续停止同一终端会话。
    /// </summary>
    [Fact]
    public void Build_ShouldGuideCliStopContinuation_WhenStopCommandIsRecommended()
    {
        var builder = new AgentRepairPromptBuilder();
        var context = new EvaluationContext
        {
            Result = "停止请求未完成",
            ToolRecords =
            [
                new ToolExecutionRecord
                {
                    ToolName = "HostService.StopCommandAsync",
                    Success = false,
                    FailureReason = ToolFailureReason.None,
                    Retryable = true,
                    SuggestedAction = ToolSuggestedAction.StopCommand,
                    ErrorSummary = "recommendedTool: HostService.StopCommandAsync"
                }
            ]
        };

        var prompt = builder.Build(context, CreateEvaluation());

        prompt.Should().Contain("primaryAction: StopCommand");
        prompt.Should().Contain("HostService.StopCommandAsync");
        prompt.Should().Contain("停止同一终端会话");
        prompt.Should().Contain("不要重新启动相同命令");
        prompt.Should().Contain("不要切换到其他工具");
    }

    /// <summary>
    /// 过长的上一次输出应被截断。
    /// </summary>
    [Fact]
    public void Build_ShouldTruncatePreviousOutput()
    {
        var builder = new AgentRepairPromptBuilder();
        var context = new EvaluationContext
        {
            Result = new string('a', 2100)
        };

        var prompt = builder.Build(context, CreateEvaluation());

        prompt.Should().Contain("... (已截断)");
        prompt.Should().NotContain(new string('a', 2100));
    }

    /// <summary>
    /// 过长的原始用户目标应被截断，避免修复提示膨胀。
    /// </summary>
    [Fact]
    public void Build_ShouldTruncateUserGoal()
    {
        var builder = new AgentRepairPromptBuilder();
        var context = new EvaluationContext
        {
            Goal = new string('目', 1100),
            Result = "上一次输出"
        };

        var prompt = builder.Build(context, CreateEvaluation());

        prompt.Should().Contain("### 原始用户目标");
        prompt.Should().Contain("... (已截断)");
        prompt.Should().NotContain(new string('目', 1100));
    }

    private static EvaluationResult CreateEvaluation()
    {
        return new EvaluationResult
        {
            Score = 72.5,
            CorrectnessScore = 70,
            CompletenessScore = 60,
            QualityScore = 80,
            EfficiencyScore = 75,
            Feedback = "缺少关键步骤",
            ImprovementSuggestions = ["补充验证证据"]
        };
    }

    private static ToolExecutionRecord CreateRepeatedFailure()
    {
        return new ToolExecutionRecord
        {
            ToolName = "HostService.WaitCommandAsync",
            Success = false,
            FailureReason = ToolFailureReason.None,
            Retryable = true,
            SuggestedAction = ToolSuggestedAction.WaitForCompletion,
            ErrorSummary = "命令仍在运行"
        };
    }
}
