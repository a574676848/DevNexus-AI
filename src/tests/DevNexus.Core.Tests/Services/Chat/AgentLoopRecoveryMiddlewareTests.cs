using DevNexus.Core.Models.Evaluation;
using DevNexus.Core.Services.Chat;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// Agent Loop 恢复中间件测试。
/// </summary>
public sealed class AgentLoopRecoveryMiddlewareTests
{
    /// <summary>
    /// 已存在挂起交互时应停止自动修复，并复用产品化摘要文案。
    /// </summary>
    [Fact]
    public async Task RuntimeRecovery_ShouldStop_WhenPendingInteractionExists()
    {
        var middleware = new RuntimeRecoveryMiddleware();
        var context = new AgentLoopRecoveryContext
        {
            Runtime = new ChatSessionRuntimeSnapshot
            {
                PendingInteractionCount = 1,
                PrimaryPendingInteractionTitle = "等待审批",
                PrimaryPendingInteractionDescription = "请先确认高风险操作。",
                PrimaryPendingInteractionSummary = new PendingInteractionSummaryDto
                {
                    Label = "等待用户审批",
                    Description = "审批完成后再继续执行。"
                }
            },
            ToolRecords = [CreateFailedRecord(ToolSuggestedAction.Retry)]
        };

        var result = await middleware.TryHandleAsync(context, CancellationToken.None);

        result.Should().NotBeNull();
        result!.ShouldStop.Should().BeTrue();
        result.StopTitle.Should().Be("等待用户审批");
        result.StopMessage.Should().Be("审批完成后再继续执行。");
    }

    /// <summary>
    /// CLI stdin 续接属于同一终端会话恢复，不应升级成产品化挂起交互。
    /// </summary>
    [Fact]
    public async Task RuntimeRecovery_ShouldNotCreatePendingInteraction_WhenCliInputCanContinue()
    {
        var middleware = new RuntimeRecoveryMiddleware();
        var context = new AgentLoopRecoveryContext
        {
            ToolRecords =
            [
                CreateFailedRecord(
                    ToolSuggestedAction.PromptUserInput,
                    requiresHumanIntervention: false)
            ]
        };

        var result = await middleware.TryHandleAsync(context, CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>
    /// 已进入重试后的不可恢复失败应停止自动修复，避免继续消耗 LLM 评估轮次。
    /// </summary>
    [Fact]
    public async Task LoopGuard_ShouldStop_WhenRetryAttemptHasOnlyUnrecoverableFailures()
    {
        var middleware = new LoopGuardMiddleware();
        var context = new AgentLoopRecoveryContext
        {
            AgentLoopAttempt = 1,
            ToolRecords =
            [
                CreateFailedRecord(
                    ToolSuggestedAction.Abort,
                    retryable: false,
                    requiresHumanIntervention: false)
            ]
        };

        var result = await middleware.TryHandleAsync(context, CancellationToken.None);

        result.Should().NotBeNull();
        result!.ShouldStop.Should().BeTrue();
        result.StopTitle.Should().Be("自动修复已停止");
        result.StopMessage.Should().Contain("不具备自动重试条件");
    }

    /// <summary>
    /// 首轮不可恢复失败仍应交给后续评估生成更完整的用户反馈。
    /// </summary>
    [Fact]
    public async Task LoopGuard_ShouldContinue_WhenFirstAttemptHasUnrecoverableFailure()
    {
        var middleware = new LoopGuardMiddleware();
        var context = new AgentLoopRecoveryContext
        {
            AgentLoopAttempt = 0,
            ToolRecords =
            [
                CreateFailedRecord(
                    ToolSuggestedAction.Abort,
                    retryable: false,
                    requiresHumanIntervention: false)
            ]
        };

        var result = await middleware.TryHandleAsync(context, CancellationToken.None);

        result.Should().BeNull();
    }

    /// <summary>
    /// 停止终端命令多次未闭环时应停止自动修复，避免在同一停止动作上空转。
    /// </summary>
    [Fact]
    public async Task LoopGuard_ShouldStop_WhenStopCommandRepeatedlyFails()
    {
        var middleware = new LoopGuardMiddleware();
        var context = new AgentLoopRecoveryContext
        {
            AgentLoopAttempt = 2,
            ToolRecords =
            [
                CreateFailedRecord(
                    ToolSuggestedAction.StopCommand,
                    retryable: true,
                    requiresHumanIntervention: false)
            ]
        };

        var result = await middleware.TryHandleAsync(context, CancellationToken.None);

        result.Should().NotBeNull();
        result!.ShouldStop.Should().BeTrue();
        result.StopTitle.Should().Be("终端停止未闭环");
        result.StopMessage.Should().Contain("已多次尝试停止同一终端会话");
    }

    /// <summary>
    /// 停止终端命令首轮续接仍应继续，让模型优先调用 StopCommandAsync 闭环。
    /// </summary>
    [Fact]
    public async Task LoopGuard_ShouldContinue_WhenStopCommandHasNotExceededContinuationBudget()
    {
        var middleware = new LoopGuardMiddleware();
        var context = new AgentLoopRecoveryContext
        {
            AgentLoopAttempt = 1,
            ToolRecords =
            [
                CreateFailedRecord(
                    ToolSuggestedAction.StopCommand,
                    retryable: true,
                    requiresHumanIntervention: false)
            ]
        };

        var result = await middleware.TryHandleAsync(context, CancellationToken.None);

        result.Should().BeNull();
    }

    private static ToolExecutionRecord CreateFailedRecord(
        ToolSuggestedAction suggestedAction,
        bool retryable = true,
        bool requiresHumanIntervention = true)
    {
        return new ToolExecutionRecord
        {
            ToolCallId = Guid.NewGuid(),
            ToolName = "HostService.ExecuteCommandAsync",
            Arguments = """{"command":"dotnet test"}""",
            Success = false,
            Retryable = retryable,
            RequiresHumanIntervention = requiresHumanIntervention,
            SuggestedAction = suggestedAction,
            FailureReason = ToolFailureReason.FatalExecutionError,
            ErrorSummary = "执行失败"
        };
    }
}
