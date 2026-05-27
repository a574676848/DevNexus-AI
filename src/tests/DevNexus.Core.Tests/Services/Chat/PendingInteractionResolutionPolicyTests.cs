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
    /// 单次审批应带 Once 授权范围。
    /// </summary>
    [Fact]
    public void Resolve_ShouldReturnOnceScope_WhenApproveOnce()
    {
        var decision = PendingInteractionResolutionPolicy.Resolve("approve-once");

        decision.Action.Should().Be(PendingInteractionResolutionActions.ApproveOnce);
        decision.ApprovalScope.Should().Be(CliApprovalGrantScope.Once);
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
    }
}
