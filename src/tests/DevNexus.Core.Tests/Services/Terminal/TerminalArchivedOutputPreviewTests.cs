using DevNexus.Core.Services.Terminal;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Terminal;

/// <summary>
/// 终端归档输出预览裁剪器测试。
/// </summary>
public sealed class TerminalArchivedOutputPreviewTests
{
    /// <summary>
    /// 短输出应保持原样。
    /// </summary>
    [Fact]
    public void Normalize_ShouldKeepShortOutput()
    {
        var output = "dotnet build passed";

        TerminalArchivedOutputPreview.Normalize(output).Should().Be(output);
    }

    /// <summary>
    /// 长输出应只保留最近内容，并标记较早内容已归档。
    /// </summary>
    [Fact]
    public void Normalize_ShouldKeepRecentOutput_WhenOutputIsTooLong()
    {
        var output = new string('a', 120_001) + "TAIL";

        var preview = TerminalArchivedOutputPreview.Normalize(output);

        preview.Should().StartWith("[较早输出已归档，当前仅保留最近内容]");
        preview.Should().EndWith("TAIL");
        preview.Length.Should().BeLessThan(output.Length);
    }

    /// <summary>
    /// 裁剪起点附近有换行时应从下一行开始，避免半行预览。
    /// </summary>
    [Fact]
    public void Normalize_ShouldPreferLineBoundary()
    {
        var output = new string('a', 30_010) + "\nRECENT" + new string('b', 89_990);

        var preview = TerminalArchivedOutputPreview.Normalize(output);

        preview.Should().Contain("RECENT");
        preview.Should().NotContain(new string('a', 20));
    }

    /// <summary>
    /// 剥离预览标记后可继续拼接增量。
    /// </summary>
    [Fact]
    public void StripBanner_ShouldRemoveTrimMarker()
    {
        var preview = TerminalArchivedOutputPreview.Normalize(new string('a', 120_001) + "TAIL");

        var stripped = TerminalArchivedOutputPreview.StripBanner(preview);

        stripped.Should().NotStartWith("[较早输出已归档");
        stripped.Should().EndWith("TAIL");
    }
}
