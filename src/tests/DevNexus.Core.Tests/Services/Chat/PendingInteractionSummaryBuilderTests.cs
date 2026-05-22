using DevNexus.Core.Services.Chat;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 挂起交互摘要构建器测试。
/// </summary>
public sealed class PendingInteractionSummaryBuilderTests
{
    /// <summary>
    /// 审批交互应提示审批动作并阻塞普通发送。
    /// </summary>
    [Fact]
    public void Build_ShouldReturnApprovalSummary()
    {
        var summary = PendingInteractionSummaryBuilder.Build(
            PendingInteractionKind.Approval,
            title: null,
            description: null);

        summary.Tone.Should().Be("warning");
        summary.Label.Should().Be("等待执行审批");
        summary.NextAction.Should().Be("ApproveOrDeny");
        summary.InputPlaceholder.Should().Be("当前等待审批，审批通过后可继续");
        summary.BlocksMessageSend.Should().BeTrue();
    }

    /// <summary>
    /// 凭证交互应突出凭证补充。
    /// </summary>
    [Fact]
    public void Build_ShouldReturnCredentialSummary()
    {
        var summary = PendingInteractionSummaryBuilder.Build(
            PendingInteractionKind.Credential,
            title: null,
            description: null);

        summary.Tone.Should().Be("danger");
        summary.Label.Should().Be("等待补充凭证");
        summary.NextAction.Should().Be("ProvideCredential");
        summary.Description.Should().Be("当前会话正在等待凭证补充，补充后才能继续执行。");
    }

    /// <summary>
    /// 自定义标题和说明应保留，用于展示真实失败原因。
    /// </summary>
    [Fact]
    public void Build_ShouldKeepCustomTitleAndDescription()
    {
        var summary = PendingInteractionSummaryBuilder.Build(
            PendingInteractionKind.Clarification,
            "需要补充参数",
            "缺少目标路径。");

        summary.Label.Should().Be("需要补充参数");
        summary.Description.Should().Be("缺少目标路径。");
        summary.NextAction.Should().Be("ProvideInput");
    }

    /// <summary>
    /// 外部授权回调应使用信息态。
    /// </summary>
    [Fact]
    public void Build_ShouldReturnInfo_WhenOAuthCallback()
    {
        var summary = PendingInteractionSummaryBuilder.Build(
            PendingInteractionKind.OAuthCallback,
            title: null,
            description: null);

        summary.Tone.Should().Be("info");
        summary.Label.Should().Be("等待外部授权完成");
        summary.NextAction.Should().Be("CompleteExternalAuthorization");
    }
}
