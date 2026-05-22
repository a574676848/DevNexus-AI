using DevNexus.Core.Services.Terminal;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Terminal;

/// <summary>
/// 终端输出预览构建器测试。
/// </summary>
public sealed class TerminalOutputPreviewBuilderTests
{
    /// <summary>
    /// 短输出应保持原样。
    /// </summary>
    [Fact]
    public void Build_ShouldKeepShortOutput()
    {
        var output = "dotnet build passed";

        var preview = TerminalOutputPreviewBuilder.Build(output, 20, 20);

        preview.Should().Be(output);
    }

    /// <summary>
    /// 长输出应保留总量信息、头部和尾部。
    /// </summary>
    [Fact]
    public void Build_ShouldKeepMetadataHeadAndTail_WhenOutputTooLong()
    {
        var output = "COMMAND" + new string('x', 200) + "ERROR";

        var preview = TerminalOutputPreviewBuilder.Build(output, 60, 60);

        preview.Should().Contain("Total terminal output chars:");
        preview.Should().Contain("Total terminal output lines:");
        preview.Should().Contain("COMMAND");
        preview.Should().Contain("ERROR");
        preview.Should().Contain("已按终端预览预算省略中间输出");
        preview.Length.Should().BeLessThanOrEqualTo(120);
    }

    /// <summary>
    /// 长单行输出应先压缩单行，避免模型只看到一行噪音。
    /// </summary>
    [Fact]
    public void Build_ShouldLimitLongSingleLine()
    {
        var output = "HEAD" + new string('x', 900) + "TAIL";

        var preview = TerminalOutputPreviewBuilder.Build(output, 800, 800);

        preview.Should().Contain("Total terminal output chars:");
        preview.Should().Contain("Preview line char budget:");
        preview.Should().Contain("HEAD");
        preview.Should().Contain("TAIL");
        preview.Should().Contain("单行输出已截断");
        preview.Length.Should().BeLessThan(output.Length);
    }

    /// <summary>
    /// 海量多行输出应保留行窗口，避免模型上下文被中间流水日志挤满。
    /// </summary>
    [Fact]
    public void Build_ShouldLimitLineWindow_WhenOutputHasTooManyLines()
    {
        var lines = Enumerable.Range(1, 180).Select(index => $"line-{index:D3}");
        var output = string.Join('\n', lines);

        var preview = TerminalOutputPreviewBuilder.Build(output, 2000, 2000);

        preview.Should().Contain("Preview line window:");
        preview.Should().Contain("line-001");
        preview.Should().Contain("line-180");
        preview.Should().Contain("已按终端预览预算省略中间输出");
        preview.Should().NotContain("line-090");
    }

    /// <summary>
    /// 零预算不应返回噪音文本。
    /// </summary>
    [Fact]
    public void Build_ShouldReturnEmpty_WhenBudgetIsZero()
    {
        var preview = TerminalOutputPreviewBuilder.Build("output", 0, 0);

        preview.Should().BeEmpty();
    }
}
