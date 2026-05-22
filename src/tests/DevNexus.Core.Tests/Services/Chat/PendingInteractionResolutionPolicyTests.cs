using DevNexus.Core.Services.Chat;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 挂起交互解决策略测试。
/// </summary>
public sealed class PendingInteractionResolutionPolicyTests
{
    /// <summary>
    /// 兼容旧 approve 动作，并归一为单次审批。
    /// </summary>
    [Fact]
    public void Resolve_ShouldNormalizeApproveToApproveOnce()
    {
        var decision = PendingInteractionResolutionPolicy.Resolve("approve");

        decision.Action.Should().Be(PendingInteractionResolutionActions.ApproveOnce);
        decision.ApprovalScope.Should().Be(CliApprovalGrantScope.Once);
        decision.ResumeMessage.Should().Be("我已允许本次命令执行，请继续。");
    }

    /// <summary>
    /// 同类命令审批应带 Pattern 授权范围。
    /// </summary>
    [Fact]
    public void Resolve_ShouldReturnPatternScope_WhenApprovePattern()
    {
        var decision = PendingInteractionResolutionPolicy.Resolve("approve-pattern");

        decision.Action.Should().Be(PendingInteractionResolutionActions.ApprovePattern);
        decision.ApprovalScope.Should().Be(CliApprovalGrantScope.Pattern);
        decision.ResumeMessage.Should().Be("我已允许当前会话中的同类命令继续执行，请继续。");
    }

    /// <summary>
    /// 拒绝动作不应携带授权范围。
    /// </summary>
    [Fact]
    public void Resolve_ShouldReturnDeniedDecision_WhenDeny()
    {
        var decision = PendingInteractionResolutionPolicy.Resolve("deny");

        decision.Action.Should().Be(PendingInteractionResolutionActions.Deny);
        decision.IsDenied.Should().BeTrue();
        decision.ApprovalScope.Should().BeNull();
    }

    /// <summary>
    /// 未知动作按普通提交处理。
    /// </summary>
    [Fact]
    public void Resolve_ShouldFallbackToSubmit_WhenUnknown()
    {
        var decision = PendingInteractionResolutionPolicy.Resolve("custom");

        decision.Action.Should().Be(PendingInteractionResolutionActions.Submit);
        decision.ApprovalScope.Should().BeNull();
        decision.ResumeMessage.Should().Be("我已补充所需信息，请继续。");
    }
}
