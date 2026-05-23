using DevNexus.Core.Services.Terminal;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Terminal;

/// <summary>
/// 终端保留缓冲切片工具测试。
/// </summary>
public sealed class TerminalRetainedOutputSliceTests
{
    /// <summary>
    /// 正常偏移应只返回新输出。
    /// </summary>
    [Fact]
    public void FromRetainedBuffer_ShouldReturnDelta_WhenStartIndexIsInsideBuffer()
    {
        var output = "before\ncurrent\n";

        TerminalRetainedOutputSlice.FromRetainedBuffer(output, "before\n".Length)
            .Should()
            .Be("current\n");
    }

    /// <summary>
    /// 偏移正好在末尾时说明没有新增输出。
    /// </summary>
    [Fact]
    public void FromRetainedBuffer_ShouldReturnEmpty_WhenStartIndexEqualsLength()
    {
        var output = "before\n";

        TerminalRetainedOutputSlice.FromRetainedBuffer(output, output.Length)
            .Should()
            .BeEmpty();
    }

    /// <summary>
    /// 旧偏移落在已裁剪历史之前时，应返回当前保留窗口，避免长输出续接丢失尾部事实。
    /// </summary>
    [Fact]
    public void FromRetainedBuffer_ShouldReturnRetainedWindow_WhenStartIndexWasTrimmedAway()
    {
        var output = TerminalRetainedOutputSlice.TrimmedHistoryMarker + "latest output\n";

        TerminalRetainedOutputSlice.FromRetainedBuffer(output, output.Length + 100)
            .Should()
            .Be(output);
    }

    /// <summary>
    /// 裁剪后如果旧偏移与当前窗口长度巧合相等，仍应返回当前保留窗口。
    /// </summary>
    [Fact]
    public void FromRetainedBuffer_ShouldReturnRetainedWindow_WhenTrimmedStartIndexEqualsLength()
    {
        var output = TerminalRetainedOutputSlice.TrimmedHistoryMarker + "latest output\n";

        TerminalRetainedOutputSlice.FromRetainedBuffer(output, output.Length)
            .Should()
            .Be(output);
    }

    /// <summary>
    /// 没有裁剪标记时，越界偏移仍应返回空串，避免重复回放旧输出。
    /// </summary>
    [Fact]
    public void FromRetainedBuffer_ShouldReturnEmpty_WhenStartIndexIsOutOfRangeWithoutTrimMarker()
    {
        TerminalRetainedOutputSlice.FromRetainedBuffer("latest output\n", 100)
            .Should()
            .BeEmpty();
    }
}
