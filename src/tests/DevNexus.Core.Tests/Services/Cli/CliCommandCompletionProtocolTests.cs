using DevNexus.Core.Services.Cli;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Cli;

/// <summary>
/// CLI 命令完成哨兵协议测试。
/// </summary>
public sealed class CliCommandCompletionProtocolTests
{
    /// <summary>
    /// Windows 命令应读取 PowerShell 的最近退出码。
    /// </summary>
    [Fact]
    public void BuildCommand_ShouldUseLastExitCode_OnWindows()
    {
        var command = CliCommandCompletionProtocol.BuildCommand("dotnet test", "S", isWindows: true);

        command.Should().Be("dotnet test; $devnexusExitCode = if ($null -ne $LASTEXITCODE) { $LASTEXITCODE } elseif ($?) { 0 } else { 1 }; echo 'S'; echo $devnexusExitCode");
    }

    /// <summary>
    /// Windows 命令应在 PowerShell cmdlet 没有 LASTEXITCODE 时回退到 $?。
    /// </summary>
    [Fact]
    public void BuildCommand_ShouldFallbackToPowerShellSuccessFlag_OnWindows()
    {
        var command = CliCommandCompletionProtocol.BuildCommand("Get-Item missing", "S", isWindows: true);

        command.Should().Contain("elseif ($?) { 0 } else { 1 }");
    }

    /// <summary>
    /// 非 Windows 命令应读取 POSIX 退出码。
    /// </summary>
    [Fact]
    public void BuildCommand_ShouldUsePosixExitCode_OnUnix()
    {
        var command = CliCommandCompletionProtocol.BuildCommand("dotnet test", "S", isWindows: false);

        command.Should().Be("dotnet test; devnexus_exit_code=$?; echo 'S'; echo $devnexus_exit_code");
    }

    /// <summary>
    /// 输出未包含哨兵时不应误判完成。
    /// </summary>
    [Fact]
    public void TryParseCompletion_ShouldReturnFalse_WhenSentinelMissing()
    {
        var completed = CliCommandCompletionProtocol.TryParseCompletion(
            "build still running",
            "S",
            out var result);

        completed.Should().BeFalse();
        result.IsCompleted.Should().BeFalse();
    }

    /// <summary>
    /// 输出包含哨兵时应返回清理后的命令输出和退出码。
    /// </summary>
    [Fact]
    public void TryParseCompletion_ShouldReturnCleanOutputAndExitCode()
    {
        var completed = CliCommandCompletionProtocol.TryParseCompletion(
            "line 1\r\nline 2\r\nS\r\n2\r\n",
            "S",
            out var result);

        completed.Should().BeTrue();
        result.IsCompleted.Should().BeTrue();
        result.CleanOutput.Should().Be("line 1\r\nline 2");
        result.ExitCode.Should().Be(2);
    }
}
