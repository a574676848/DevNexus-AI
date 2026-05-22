using DevNexus.Core.Services.Terminal;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Terminal;

/// <summary>
/// 终端输出观察摘要构建器测试。
/// </summary>
public sealed class TerminalOutputWatchSummaryBuilderTests
{
    /// <summary>
    /// 增量输出应识别关键观察标签。
    /// </summary>
    [Fact]
    public void DetectLabels_ShouldDetectKnownTerminalSignals()
    {
        var output = string.Join(
            '\n',
            "warning: package restore fallback",
            "fatal: build failed",
            "permission denied",
            "Press Enter to continue");

        var labels = TerminalOutputWatchSummaryBuilder.DetectLabels(output);

        labels.Should().Equal(
            "检测到错误输出",
            "检测到警告输出",
            "检测到权限或审批拦截",
            "检测到交互输入提示");
    }

    /// <summary>
    /// 观察摘要应去重并保留首次出现顺序。
    /// </summary>
    [Fact]
    public void Build_ShouldDeduplicateLabels()
    {
        var summary = TerminalOutputWatchSummaryBuilder.Build(
        [
            "检测到错误输出",
            "检测到警告输出",
            "检测到错误输出"
        ]);

        summary.Should().Be("检测到错误输出；检测到警告输出");
    }

    /// <summary>
    /// 合并摘要时不应重复已有标签。
    /// </summary>
    [Fact]
    public void Merge_ShouldDeduplicateExistingAndNextSummary()
    {
        var summary = TerminalOutputWatchSummaryBuilder.Merge(
            "检测到错误输出；检测到警告输出",
            "检测到警告输出；检测到交互输入提示");

        summary.Should().Be("检测到错误输出；检测到警告输出；检测到交互输入提示");
    }

    /// <summary>
    /// 换行统计应只统计实际换行符数量。
    /// </summary>
    [Fact]
    public void CountNewLines_ShouldCountLineFeedCharacters()
    {
        TerminalOutputWatchSummaryBuilder.CountNewLines("a\nb\r\nc").Should().Be(2);
        TerminalOutputWatchSummaryBuilder.CountNewLines(string.Empty).Should().Be(0);
    }
}
