using DevNexus.Core.Models.Evaluation;
using DevNexus.Core.Services.Chat;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// Agent Loop 恢复前置判断服务测试。
/// </summary>
public sealed class AgentLoopRecoveryGuardTests
{
    /// <summary>
    /// 已存在挂起交互时应优先停止自动修复，即使同轮工具记录存在协议异常。
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_ShouldPreferPendingInteraction_WhenToolSequenceIsInvalid()
    {
        var guard = new AgentLoopRecoveryGuard(
            new StubRuntimeInspector(),
            new AgentLoopRecoveryPipeline(
            [
                new RuntimeRecoveryMiddleware(),
                new LoopGuardMiddleware()
            ]));

        var result = await guard.EvaluateAsync(
            userId: Guid.NewGuid(),
            sessionId: Guid.NewGuid(),
            toolRecords:
            [
                new ToolExecutionRecord
                {
                    ToolName = "HostService.ExecuteCommandAsync",
                    Arguments = "{}",
                    Success = false,
                    SuggestedAction = ToolSuggestedAction.Retry,
                    FailureReason = ToolFailureReason.ToolFormatError
                }
            ],
            agentLoopAttempt: 1,
            CancellationToken.None);

        result.ShouldStop.Should().BeTrue();
        result.StopTitle.Should().Be("等待用户审批");
        result.StopMessage.Should().Be("请先处理当前审批。");
    }

    private sealed class StubRuntimeInspector : IChatSessionRuntimeInspector
    {
        public Task<ChatSessionRuntimeSnapshot> InspectAsync(
            Guid userId,
            Guid sessionId,
            int queuedCount,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ChatSessionRuntimeSnapshot
            {
                PendingInteractionCount = 1,
                PrimaryPendingInteractionSummary = new PendingInteractionSummaryDto
                {
                    Label = "等待用户审批",
                    Description = "请先处理当前审批。"
                }
            });
        }
    }
}
