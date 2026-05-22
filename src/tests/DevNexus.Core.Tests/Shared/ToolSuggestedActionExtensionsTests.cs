using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Shared;

/// <summary>
/// 工具建议动作扩展测试。
/// </summary>
public sealed class ToolSuggestedActionExtensionsTests
{
    /// <summary>
    /// 恢复优先级应保持稳定。
    /// </summary>
    [Fact]
    public void GetRecoveryPriority_ShouldReturnStablePriority()
    {
        var priority = ToolSuggestedActionExtensions.GetRecoveryPriority();

        priority.Should().Equal(
            ToolSuggestedAction.RequestApproval,
            ToolSuggestedAction.PromptUserInput,
            ToolSuggestedAction.RefreshCredential,
            ToolSuggestedAction.StopCommand,
            ToolSuggestedAction.WaitForCompletion,
            ToolSuggestedAction.Retry,
            ToolSuggestedAction.Fallback,
            ToolSuggestedAction.Abort);
    }

    /// <summary>
    /// 诊断短文本应覆盖所有建议动作。
    /// </summary>
    [Fact]
    public void ToDiagnosticText_ShouldMapAllSuggestedActions()
    {
        ToolSuggestedAction.None.ToDiagnosticText().Should().Be("无需动作");
        ToolSuggestedAction.RequestApproval.ToDiagnosticText().Should().Be("等待审批");
        ToolSuggestedAction.PromptUserInput.ToDiagnosticText().Should().Be("补充输入");
        ToolSuggestedAction.RefreshCredential.ToDiagnosticText().Should().Be("刷新凭证");
        ToolSuggestedAction.StopCommand.ToDiagnosticText().Should().Be("停止命令");
        ToolSuggestedAction.WaitForCompletion.ToDiagnosticText().Should().Be("等待完成");
        ToolSuggestedAction.Retry.ToDiagnosticText().Should().Be("建议重试");
        ToolSuggestedAction.Fallback.ToDiagnosticText().Should().Be("建议降级");
        ToolSuggestedAction.Abort.ToDiagnosticText().Should().Be("建议终止");
    }
}
