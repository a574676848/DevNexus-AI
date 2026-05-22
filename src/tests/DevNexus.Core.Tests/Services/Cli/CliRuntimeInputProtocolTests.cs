using DevNexus.Core.Services.Cli;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Cli;

/// <summary>
/// CLI 运行时输入协议测试。
/// </summary>
public sealed class CliRuntimeInputProtocolTests
{
    /// <summary>
    /// 输入协议应移除末尾换行，避免宿主额外收到空回车。
    /// </summary>
    [Fact]
    public void Build_ShouldTrimTrailingLineBreaks()
    {
        var envelope = CliRuntimeInputProtocol.Build("yes\r\n");

        envelope.Input.Should().Be("yes");
        envelope.ModelVisiblePreview.Should().Be("[stdin] yes");
        envelope.OriginalLength.Should().Be(5);
        envelope.IsBlankLine.Should().BeFalse();
    }

    /// <summary>
    /// 空输入仍是合法 stdin 空行。
    /// </summary>
    [Fact]
    public void Build_ShouldRepresentBlankLine()
    {
        var envelope = CliRuntimeInputProtocol.Build(string.Empty);

        envelope.Input.Should().BeEmpty();
        envelope.ModelVisiblePreview.Should().Be("[stdin] 空行");
        envelope.IsBlankLine.Should().BeTrue();
    }

    /// <summary>
    /// 多行输入摘要应使用单行预览。
    /// </summary>
    [Fact]
    public void Build_ShouldCompactMultilinePreview()
    {
        var envelope = CliRuntimeInputProtocol.Build("first\nsecond");

        envelope.Input.Should().Be("first\nsecond");
        envelope.ModelVisiblePreview.Should().Be("[stdin] first\\nsecond");
    }

    /// <summary>
    /// 过长输入摘要应截断，避免污染运行态展示。
    /// </summary>
    [Fact]
    public void Build_ShouldTruncateLongPreview()
    {
        var envelope = CliRuntimeInputProtocol.Build(new string('a', 200));

        envelope.Input.Should().HaveLength(200);
        envelope.ModelVisiblePreview.Should().StartWith("[stdin] ");
        envelope.ModelVisiblePreview.Should().EndWith("...");
        envelope.ModelVisiblePreview.Length.Should().Be(131);
    }
}
