using DevNexus.Core.Services.Terminal;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Terminal;

/// <summary>
/// 终端日志分块输出切片工具测试。
/// </summary>
public sealed class TerminalLogChunkOutputSliceTests
{
    /// <summary>
    /// live 输出已经由进程注册表按 startIndex 切片，不应再次切片。
    /// </summary>
    [Fact]
    public void FromSources_ShouldReturnLiveSliceDirectly_WhenLiveOutputAlreadySliced()
    {
        var result = TerminalLogChunkOutputSlice.FromSources(
            archivedOutput: "archived-older\narchived-newer\n",
            archivedPlainOutput: "archived-older\narchived-newer\n",
            liveOutput: "live-newer\n",
            livePlainOutput: "live-newer\n",
            startIndex: 120,
            useArchivedOutput: false);

        result.Output.Should().Be("live-newer\n");
        result.PlainOutput.Should().Be("live-newer\n");
    }

    /// <summary>
    /// 归档输出是完整事实源，仍应按 startIndex 返回增量。
    /// </summary>
    [Fact]
    public void FromSources_ShouldSliceArchivedOutput_WhenUsingArchivedOutput()
    {
        var output = "archived-older\narchived-newer\n";

        var result = TerminalLogChunkOutputSlice.FromSources(
            archivedOutput: output,
            archivedPlainOutput: output,
            liveOutput: "live-newer\n",
            livePlainOutput: "live-newer\n",
            startIndex: "archived-older\n".Length,
            useArchivedOutput: true);

        result.Output.Should().Be("archived-newer\n");
        result.PlainOutput.Should().Be("archived-newer\n");
    }

    /// <summary>
    /// 并发 session 应各自按来源处理偏移，避免 live 偏移污染 archived 切片。
    /// </summary>
    [Fact]
    public void FromSources_ShouldKeepSliceIsolation_WhenConcurrentSessionsUseDifferentSources()
    {
        var firstArchivedOutput = "session-a-old\nsession-a-new\n";
        var first = TerminalLogChunkOutputSlice.FromSources(
            archivedOutput: firstArchivedOutput,
            archivedPlainOutput: firstArchivedOutput,
            liveOutput: "session-a-live-new\n",
            livePlainOutput: "session-a-live-new\n",
            startIndex: "session-a-old\n".Length,
            useArchivedOutput: true);
        var second = TerminalLogChunkOutputSlice.FromSources(
            archivedOutput: "session-b-old\nsession-b-new\n",
            archivedPlainOutput: "session-b-old\nsession-b-new\n",
            liveOutput: "session-b-live-new\n",
            livePlainOutput: "session-b-live-new\n",
            startIndex: firstArchivedOutput.Length + 100,
            useArchivedOutput: false);

        first.Output.Should().Be("session-a-new\n");
        first.PlainOutput.Should().Be("session-a-new\n");
        second.Output.Should().Be("session-b-live-new\n");
        second.PlainOutput.Should().Be("session-b-live-new\n");
    }
}
