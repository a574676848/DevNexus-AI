using DevNexus.Core.Services.Cli;
using DevNexus.Shared.Constants;
using DevNexus.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Cli;

/// <summary>
/// CLI 运行时状态摘要构建器测试。
/// </summary>
public sealed class CliRuntimeStatusSummaryBuilderTests
{
    /// <summary>
    /// 等待输入应优先于普通运行态。
    /// </summary>
    [Fact]
    public void Build_ShouldPrioritizeWaitingForInput()
    {
        var summary = CliRuntimeStatusSummaryBuilder.Build(
            CliExecStatus.Running,
            waitingForInput: true,
            CliSessionTerminationReasons.None);

        summary.Tone.Should().Be("waiting");
        summary.Label.Should().Be("等待输入");
        summary.NextAction.Should().Be("SendInput");
        summary.RequiresInput.Should().BeTrue();
        summary.IsTerminal.Should().BeFalse();
    }

    /// <summary>
    /// 运行中应提示继续查看输出。
    /// </summary>
    [Fact]
    public void Build_ShouldReturnActive_WhenRunning()
    {
        var summary = CliRuntimeStatusSummaryBuilder.Build(
            CliExecStatus.Running,
            waitingForInput: false,
            CliSessionTerminationReasons.None);

        summary.Tone.Should().Be("active");
        summary.Label.Should().Be("运行中");
        summary.NextAction.Should().Be("WatchOutput");
    }

    /// <summary>
    /// 成功完成应提示查看结果。
    /// </summary>
    [Fact]
    public void Build_ShouldReturnSuccess_WhenCompleted()
    {
        var summary = CliRuntimeStatusSummaryBuilder.Build(
            CliExecStatus.Completed,
            waitingForInput: false,
            CliSessionTerminationReasons.Completed);

        summary.Tone.Should().Be("success");
        summary.Label.Should().Be("已完成");
        summary.NextAction.Should().Be("ReviewResult");
        summary.IsTerminal.Should().BeTrue();
        summary.TerminationReasonText.Should().Be("正常结束");
    }

    /// <summary>
    /// 进程异常退出应先提示复盘输出，不默认引导回滚。
    /// </summary>
    [Fact]
    public void Build_ShouldReturnReviewResult_WhenProcessExited()
    {
        var summary = CliRuntimeStatusSummaryBuilder.Build(
            CliExecStatus.Failed,
            waitingForInput: false,
            CliSessionTerminationReasons.ProcessExited);

        summary.Tone.Should().Be("danger");
        summary.Label.Should().Be("失败");
        summary.NextAction.Should().Be("ReviewResult");
        summary.TerminationReasonText.Should().Be("进程已退出");
    }

    /// <summary>
    /// 运行时错误仍应保留回滚入口，便于高风险文件命令恢复 checkpoint。
    /// </summary>
    [Fact]
    public void Build_ShouldReturnRollback_WhenRuntimeErrorFailed()
    {
        var summary = CliRuntimeStatusSummaryBuilder.Build(
            CliExecStatus.Failed,
            waitingForInput: false,
            CliSessionTerminationReasons.Error);

        summary.Tone.Should().Be("danger");
        summary.Label.Should().Be("失败");
        summary.NextAction.Should().Be("Rollback");
        summary.TerminationReasonText.Should().Be("执行失败");
    }

    /// <summary>
    /// 超时应提示缩小范围后重试。
    /// </summary>
    [Fact]
    public void Build_ShouldReturnWarning_WhenTimedOut()
    {
        var summary = CliRuntimeStatusSummaryBuilder.Build(
            CliExecStatus.TimedOut,
            waitingForInput: false,
            CliSessionTerminationReasons.MaxRuntimeExceeded);

        summary.Tone.Should().Be("warning");
        summary.Label.Should().Be("已超时");
        summary.NextAction.Should().Be("Retry");
        summary.TerminationReasonText.Should().Be("执行超时");
    }

    /// <summary>
    /// 回滚完成仍属于可复盘的成功终态。
    /// </summary>
    [Fact]
    public void Build_ShouldReturnSuccess_WhenRolledBack()
    {
        var summary = CliRuntimeStatusSummaryBuilder.Build(
            CliExecStatus.RolledBack,
            waitingForInput: false,
            CliSessionTerminationReasons.Completed);

        summary.Tone.Should().Be("success");
        summary.Label.Should().Be("已回滚");
        summary.IsTerminal.Should().BeTrue();
    }
}
