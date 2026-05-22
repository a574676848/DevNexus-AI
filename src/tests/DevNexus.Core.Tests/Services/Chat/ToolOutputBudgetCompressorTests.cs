using DevNexus.Core.Services.Chat;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 工具输出预算压缩器测试。
/// </summary>
public sealed class ToolOutputBudgetCompressorTests
{
    /// <summary>
    /// 短输出不应被改写。
    /// </summary>
    [Fact]
    public void Compress_ShouldKeepShortOutput()
    {
        var output = "执行完成";

        var compressed = ToolOutputBudgetCompressor.Compress(output, 100);

        compressed.Should().Be(output);
    }

    /// <summary>
    /// 长输出应保留总量信息、头部和尾部。
    /// </summary>
    [Fact]
    public void Compress_ShouldKeepMetadataHeadAndTail_WhenOutputTooLong()
    {
        var output = "START-" + new string('x', 300) + "-END";

        var compressed = ToolOutputBudgetCompressor.Compress(output, 120);

        compressed.Should().Contain("Total output chars:");
        compressed.Should().Contain("Total output lines:");
        compressed.Should().Contain("START-");
        compressed.Should().Contain("-END");
        compressed.Should().Contain("已按模型可见预算省略中间内容");
        compressed.Length.Should().BeLessThanOrEqualTo(120);
    }
}
