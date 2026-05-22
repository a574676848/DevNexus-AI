using DevNexus.Core.Services.Tools;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Tools;

/// <summary>
/// 工具调用名称解析器测试。
/// </summary>
public sealed class ToolInvocationNameParserTests
{
    /// <summary>
    /// 标准 Plugin.Function 名称应解析出插件和函数。
    /// </summary>
    [Fact]
    public void Parse_ShouldReturnPluginAndFunction_WhenStandardName()
    {
        var result = ToolInvocationNameParser.Parse("WebSearchPlugin.Search");

        result.PluginName.Should().Be("WebSearchPlugin");
        result.FunctionName.Should().Be("Search");
    }

    /// <summary>
    /// Provider 包装前缀不应被当作插件名。
    /// </summary>
    [Theory]
    [InlineData("functions.WebSearchPlugin.Search")]
    [InlineData("tools.WebSearchPlugin.Search")]
    public void Parse_ShouldSkipWrapperPrefix(string toolName)
    {
        var result = ToolInvocationNameParser.Parse(toolName);

        result.PluginName.Should().Be("WebSearchPlugin");
        result.FunctionName.Should().Be("Search");
    }

    /// <summary>
    /// 单段工具名仍应保留为插件名。
    /// </summary>
    [Fact]
    public void Parse_ShouldKeepSingleSegmentAsPluginName()
    {
        var result = ToolInvocationNameParser.Parse("web-search");

        result.PluginName.Should().Be("web-search");
        result.FunctionName.Should().BeNull();
    }
}
