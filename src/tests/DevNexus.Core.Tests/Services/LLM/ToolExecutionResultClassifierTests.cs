using DevNexus.Core.Services.Chat;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 工具执行结果分类器测试。
/// </summary>
public sealed class ToolExecutionResultClassifierTests
{
    /// <summary>
    /// 运行中终端信息应归类为等待完成，而不是普通成功。
    /// </summary>
    [Fact]
    public void Classify_ShouldReturnWaitForCompletion_WhenCommandStillRunning()
    {
        var result = ToolExecutionResultClassifier.Classify(
            "[INFO] 命令仍在运行，已保留终端会话。\n[StillRunning] 本次等待预算已耗尽。");

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be(ToolFailureReason.None);
        result.SuggestedAction.Should().Be(ToolSuggestedAction.WaitForCompletion);
        result.Retryable.Should().BeTrue();
    }

    /// <summary>
    /// recommendedTool 推荐等待时应归类为等待完成。
    /// </summary>
    [Fact]
    public void Classify_ShouldReturnWaitForCompletion_WhenRecommendedToolIsWait()
    {
        var result = ToolExecutionResultClassifier.Classify(
            "[INFO] 等待终端命令完成：运行中\nrecommendedTool: HostService.WaitCommandAsync");

        result.Success.Should().BeFalse();
        result.SuggestedAction.Should().Be(ToolSuggestedAction.WaitForCompletion);
        result.Retryable.Should().BeTrue();
    }

    /// <summary>
    /// 等待输入终端信息应归类为补充 stdin，而不是普通成功。
    /// </summary>
    [Fact]
    public void Classify_ShouldReturnPromptUserInput_WhenCliWaitsForInput()
    {
        var result = ToolExecutionResultClassifier.Classify(
            "[INFO] 等待终端命令完成：等待输入\nwaitingForInput: true\nnextAction: SendInput");

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be(ToolFailureReason.MissingUserInput);
        result.SuggestedAction.Should().Be(ToolSuggestedAction.PromptUserInput);
        result.RequestedUserInputLabel.Should().Be("终端输入");
        result.UserMessage.Should().Contain("HostService.SendCommandInputAsync");
    }

    /// <summary>
    /// recommendedTool 推荐 stdin 时应归类为终端输入续接。
    /// </summary>
    [Fact]
    public void Classify_ShouldReturnPromptUserInput_WhenRecommendedToolIsSendInput()
    {
        var result = ToolExecutionResultClassifier.Classify(
            "[INFO] 等待终端命令完成：需要输入\nrecommendedTool: HostService.SendCommandInputAsync");

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be(ToolFailureReason.MissingUserInput);
        result.SuggestedAction.Should().Be(ToolSuggestedAction.PromptUserInput);
        result.RequiresHumanIntervention.Should().BeFalse();
        result.RequestedUserInputLabel.Should().Be("终端输入");
    }

    /// <summary>
    /// recommendedTool 推荐停止命令时应归类为继续停止同一会话，而不是普通降级。
    /// </summary>
    [Fact]
    public void Classify_ShouldReturnStopCommand_WhenRecommendedToolIsStop()
    {
        var result = ToolExecutionResultClassifier.Classify(
            "[FAILURE] 停止终端命令：停止请求未完成\nisActive: True\nrecommendedTool: HostService.StopCommandAsync");

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be(ToolFailureReason.None);
        result.SuggestedAction.Should().Be(ToolSuggestedAction.StopCommand);
        result.Retryable.Should().BeTrue();
        result.ShouldFallback.Should().BeFalse();
        result.UserMessage.Should().Contain("HostService.StopCommandAsync");
    }

    /// <summary>
    /// 停止命令找不到会话时不应再次推荐停止，避免进入无效停止循环。
    /// </summary>
    [Fact]
    public void Classify_ShouldFallback_WhenStopCommandSessionMissing()
    {
        var result = ToolExecutionResultClassifier.Classify(
            "[FAILURE] 停止终端命令：当前终端会话不存在或已结束。\n" +
            "alreadyExited: True\nrecommendedTool: ReviewResult");

        result.Success.Should().BeFalse();
        result.SuggestedAction.Should().Be(ToolSuggestedAction.Fallback);
        result.Retryable.Should().BeFalse();
    }

    /// <summary>
    /// 默认模式应兼容检索类工具的自然文本输出。
    /// </summary>
    [Fact]
    public void Classify_ShouldKeepPlainTextSuccess_WhenTagIsNotRequired()
    {
        var result = ToolExecutionResultClassifier.Classify("找到 3 条相关结果。");

        result.Success.Should().BeTrue();
        result.FailureReason.Should().Be(ToolFailureReason.None);
    }

    /// <summary>
    /// 受控执行工具缺少统一标签时应归类为工具格式错误。
    /// </summary>
    [Fact]
    public void Classify_ShouldReturnToolFormatError_WhenRequiredTagMissing()
    {
        var result = ToolExecutionResultClassifier.Classify(
            "命令执行完成。",
            requireTaggedOutput: true);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be(ToolFailureReason.ToolFormatError);
        result.SuggestedAction.Should().Be(ToolSuggestedAction.Retry);
        result.UserMessage.Should().Contain("[SUCCESS]");
    }
}
