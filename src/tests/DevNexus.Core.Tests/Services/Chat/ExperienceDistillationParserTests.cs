using DevNexus.Core.Services.Chat;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Chat;

/// <summary>
/// 系统经验提纯输出解析器测试。
/// </summary>
public sealed class ExperienceDistillationParserTests
{
    /// <summary>
    /// NONE 输出应被识别为无价值。
    /// </summary>
    [Fact]
    public void Parse_ShouldReturnNoValue_WhenModelOutputsNone()
    {
        var result = ExperienceDistillationParser.Parse(ExperienceDistillationOutputProtocol.NoValueMarker);

        result.HasValue.Should().BeFalse();
        result.Reason.Should().Be(ExperienceDistillationParseReasons.NoValue);
    }

    /// <summary>
    /// NONE 后混入正文时应拒绝，避免把拒绝解释误保存。
    /// </summary>
    [Fact]
    public void Parse_ShouldReject_WhenNoneContainsExtraContent()
    {
        var result = ExperienceDistillationParser.Parse("NONE\n但这里还有解释文本");

        result.HasValue.Should().BeFalse();
        result.Reason.Should().Be(ExperienceDistillationParseReasons.NoValueWithContent);
    }

    /// <summary>
    /// 合法输出应解析意图和 SOP。
    /// </summary>
    [Fact]
    public void Parse_ShouldExtractIntentAndSop_WhenOutputIsValid()
    {
        var result = ExperienceDistillationParser.Parse("""
            [INTENT]修复 .NET 构建失败
            1. 先运行 dotnet build。
            2. 根据错误定位项目。
            """);

        result.HasValue.Should().BeTrue();
        result.Intent.Should().Be("修复 .NET 构建失败");
        result.SolutionSop.Should().Contain("dotnet build");
        result.Reason.Should().Be(ExperienceDistillationParseReasons.ValueExtracted);
    }

    /// <summary>
    /// 缺少 SOP 时不应保存经验。
    /// </summary>
    [Fact]
    public void Parse_ShouldReturnMissingSop_WhenOnlyIntentExists()
    {
        var result = ExperienceDistillationParser.Parse("[INTENT]修复构建失败");

        result.HasValue.Should().BeFalse();
        result.Reason.Should().Be(ExperienceDistillationParseReasons.MissingSop);
    }

    /// <summary>
    /// 缺少意图标记时不应宽松保存。
    /// </summary>
    [Fact]
    public void Parse_ShouldReject_WhenIntentMarkerIsMissing()
    {
        var result = ExperienceDistillationParser.Parse("""
            修复 .NET 构建失败
            1. 先运行 dotnet build。
            """);

        result.HasValue.Should().BeFalse();
        result.Reason.Should().Be(ExperienceDistillationParseReasons.MissingIntentMarker);
    }

    /// <summary>
    /// Markdown 代码块输出不符合提纯协议。
    /// </summary>
    [Fact]
    public void Parse_ShouldReject_WhenMarkdownCodeBlockExists()
    {
        var result = ExperienceDistillationParser.Parse("""
            [INTENT]修复 .NET 构建失败
            ```text
            1. 先运行 dotnet build。
            ```
            """);

        result.HasValue.Should().BeFalse();
        result.Reason.Should().Be(ExperienceDistillationParseReasons.MarkdownCodeBlock);
    }

    /// <summary>
    /// SOP 超过持久化上限时应拒绝，避免长期经验膨胀。
    /// </summary>
    [Fact]
    public void Parse_ShouldReject_WhenSopIsTooLong()
    {
        var longSop = new string('a', ExperienceDistillationOutputProtocol.MaximumSopCharacters + 1);

        var result = ExperienceDistillationParser.Parse($"""
            [INTENT]治理长期记忆
            {longSop}
            """);

        result.HasValue.Should().BeFalse();
        result.Reason.Should().Be(ExperienceDistillationParseReasons.SopTooLong);
    }

    /// <summary>
    /// SOP 混入原始 QA 或工具输出标记时应拒绝。
    /// </summary>
    [Fact]
    public void Parse_ShouldReject_WhenRawTranscriptLeaksIntoSop()
    {
        var result = ExperienceDistillationParser.Parse("""
            [INTENT]治理长期记忆
            1. 先阅读【用户问题】原文。
            2. 再复制 [SUCCESS] 工具输出。
            """);

        result.HasValue.Should().BeFalse();
        result.Reason.Should().Be(ExperienceDistillationParseReasons.RawTranscriptLeak);
    }

    /// <summary>
    /// 空输出不应保存经验。
    /// </summary>
    [Fact]
    public void Parse_ShouldReturnEmpty_WhenContentIsBlank()
    {
        var result = ExperienceDistillationParser.Parse(" ");

        result.HasValue.Should().BeFalse();
        result.Reason.Should().Be(ExperienceDistillationParseReasons.Empty);
    }
}
