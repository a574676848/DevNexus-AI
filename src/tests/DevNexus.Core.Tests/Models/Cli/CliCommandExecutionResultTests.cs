using DevNexus.Core.Models.Cli;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Models.Cli;

/// <summary>
/// CLI 命令执行等待结果测试。
/// </summary>
public sealed class CliCommandExecutionResultTests
{
    /// <summary>
    /// 等待预算耗尽但命令仍在运行时，不应被当成终态。
    /// </summary>
    [Fact]
    public void IsTerminal_ShouldBeFalse_WhenCommandStillRunning()
    {
        var result = new CliCommandExecutionResult(
            "still running",
            0,
            CliCommandExecutionState.StillRunning);

        result.IsTerminal.Should().BeFalse();
    }

    /// <summary>
    /// 已完成、失败、取消和进程不可用都应视为终态。
    /// </summary>
    [Theory]
    [InlineData(CliCommandExecutionState.Completed)]
    [InlineData(CliCommandExecutionState.Failed)]
    [InlineData(CliCommandExecutionState.Cancelled)]
    [InlineData(CliCommandExecutionState.ProcessUnavailable)]
    public void IsTerminal_ShouldBeTrue_WhenCommandReachedTerminalState(
        CliCommandExecutionState state)
    {
        var result = new CliCommandExecutionResult("output", -1, state);

        result.IsTerminal.Should().BeTrue();
    }
}
