using DevNexus.Core.Services.Tools;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Tools;

/// <summary>
/// 工具目录暴露策略扩展测试。
/// </summary>
public sealed class ToolCatalogExposureExtensionsTests
{
    /// <summary>
    /// 直接暴露工具应按插件名稳定排序。
    /// </summary>
    [Fact]
    public void DirectTools_ShouldFilterAndSortDirectExposure()
    {
        var tools = CreateTools();

        var directTools = tools.DirectTools();

        directTools.Select(tool => tool.PluginName).Should().Equal("KnowledgeBasePlugin", "WebSearchPlugin");
        directTools.Should().OnlyContain(tool => tool.ExposureMode == AiOptimizationConstants.ToolExposureModes.Direct);
    }

    /// <summary>
    /// 延迟暴露工具应按插件名稳定排序。
    /// </summary>
    [Fact]
    public void DeferredTools_ShouldFilterAndSortDeferredExposure()
    {
        var tools = CreateTools();

        var deferredTools = tools.DeferredTools();

        deferredTools.Select(tool => tool.PluginName).Should().Equal("CodeExecution", "HostService");
        deferredTools.Should().OnlyContain(tool => tool.ExposureMode == AiOptimizationConstants.ToolExposureModes.Deferred);
    }

    /// <summary>
    /// 直接和延迟暴露集合应互不重叠。
    /// </summary>
    [Fact]
    public void ExposureSets_ShouldNotOverlap()
    {
        var tools = CreateTools();

        var directTools = tools.DirectTools();
        var deferredTools = tools.DeferredTools();

        directTools.Select(tool => tool.PluginName)
            .Should()
            .NotIntersectWith(deferredTools.Select(tool => tool.PluginName));
        (directTools.Count + deferredTools.Count).Should().Be(tools.Count);
    }

    /// <summary>
    /// 只读检索类直接工具可标记为支持并行执行。
    /// </summary>
    [Fact]
    public void DirectTools_ShouldKeepParallelSupportMetadata()
    {
        var tools = CreateTools();

        var directTools = tools.DirectTools();

        directTools
            .Where(tool => tool.PluginName is "KnowledgeBasePlugin" or "WebSearchPlugin")
            .Should()
            .OnlyContain(tool => tool.SupportsParallelExecution);
    }

    /// <summary>
    /// 工具名解析应容忍大小写、空格、连字符和下划线差异。
    /// </summary>
    [Theory]
    [InlineData("web-search", "WebSearchPlugin")]
    [InlineData("web_search", "WebSearchPlugin")]
    [InlineData("WEB SEARCH", "WebSearchPlugin")]
    [InlineData("knowledge-base", "KnowledgeBasePlugin")]
    [InlineData("knowledge_base", "KnowledgeBasePlugin")]
    [InlineData("code_execution", "CodeExecution")]
    public void ResolvePluginName_ShouldNormalizeCommonSeparatorDrift(
        string requestedName,
        string expectedPluginName)
    {
        var tools = CreateTools();

        var resolvedName = tools.ResolvePluginName(requestedName);

        resolvedName.Should().Be(expectedPluginName);
    }

    /// <summary>
    /// 工具名解析可使用展示名称匹配目录项。
    /// </summary>
    [Fact]
    public void ResolvePluginName_ShouldMatchDisplayName()
    {
        var tools = CreateTools();

        var resolvedName = tools.ResolvePluginName("知识库检索");

        resolvedName.Should().Be("KnowledgeBasePlugin");
    }

    /// <summary>
    /// 工具名解析可使用显式别名匹配真实插件名。
    /// </summary>
    [Fact]
    public void ResolvePluginName_ShouldMatchAliases()
    {
        var tools = CreateTools();

        var resolvedName = tools.ResolvePluginName("web-search");

        resolvedName.Should().Be("WebSearchPlugin");
    }

    /// <summary>
    /// Provider 将工具名压成单段时，显式别名加低风险分隔符仍应解析为真实插件。
    /// </summary>
    [Theory]
    [InlineData("WebSearchPlugin_Search", "WebSearchPlugin")]
    [InlineData("web-search-Search", "WebSearchPlugin")]
    [InlineData("knowledge_base_Search", "KnowledgeBasePlugin")]
    public void ResolvePluginName_ShouldMatchInvocationPrefixWithSafeSeparator(
        string requestedName,
        string expectedPluginName)
    {
        var tools = CreateTools();

        var resolvedName = tools.ResolvePluginName(requestedName);

        resolvedName.Should().Be(expectedPluginName);
    }

    /// <summary>
    /// 没有分隔符的拼接名称不属于可纠偏范围，避免猜错工具。
    /// </summary>
    [Fact]
    public void ResolvePluginName_ShouldReturnNull_WhenInvocationPrefixHasNoSeparator()
    {
        var tools = CreateTools();

        var resolvedName = tools.ResolvePluginName("WebSearchPluginSearch");

        resolvedName.Should().BeNull();
    }

    /// <summary>
    /// 工具名解析遇到歧义时不应猜测。
    /// </summary>
    [Fact]
    public void ResolvePluginName_ShouldReturnNull_WhenNameIsAmbiguous()
    {
        var tools = new[]
        {
            CreateTool("WebSearchPlugin", AiOptimizationConstants.ToolExposureModes.Direct, displayName: "Search"),
            CreateTool("Search", AiOptimizationConstants.ToolExposureModes.Direct)
        };

        var resolvedName = tools.ResolvePluginName("search");

        resolvedName.Should().BeNull();
    }

    private static IReadOnlyList<ToolCatalogItemDto> CreateTools()
    {
        return
        [
            CreateTool("HostService", AiOptimizationConstants.ToolExposureModes.Deferred),
            CreateTool(
                "WebSearchPlugin",
                AiOptimizationConstants.ToolExposureModes.Direct,
                supportsParallelExecution: true,
                aliases: ["WebSearch", "web_search", "web-search"]),
            CreateTool("CodeExecution", AiOptimizationConstants.ToolExposureModes.Deferred),
            CreateTool(
                "KnowledgeBasePlugin",
                AiOptimizationConstants.ToolExposureModes.Direct,
                displayName: "知识库检索",
                supportsParallelExecution: true,
                aliases: ["KnowledgeBase", "knowledge_base", "knowledge-base"])
        ];
    }

    private static ToolCatalogItemDto CreateTool(
        string pluginName,
        string exposureMode,
        string? displayName = null,
        bool supportsParallelExecution = false,
        IReadOnlyList<string>? aliases = null)
    {
        return new ToolCatalogItemDto
        {
            PluginName = pluginName,
            DisplayName = displayName ?? pluginName,
            Category = AiOptimizationConstants.ToolCategories.Research,
            RiskLevel = AiOptimizationConstants.ToolRiskLevels.Low,
            ExposureMode = exposureMode,
            ResultContract = "测试工具契约",
            SupportsParallelExecution = supportsParallelExecution,
            Aliases = aliases ?? Array.Empty<string>()
        };
    }
}
