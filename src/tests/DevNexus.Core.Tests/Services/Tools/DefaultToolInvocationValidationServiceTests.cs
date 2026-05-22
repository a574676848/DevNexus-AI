using DevNexus.Core.Abstractions;
using DevNexus.Core.Services.Tools;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using FluentAssertions;
using Xunit;

namespace DevNexus.Core.Tests.Services.Tools;

/// <summary>
/// 默认工具调用参数预验证服务测试。
/// </summary>
public sealed class DefaultToolInvocationValidationServiceTests
{
    /// <summary>
    /// 查询类工具别名应进入空查询参数校验。
    /// </summary>
    [Fact]
    public void Validate_ShouldUseCatalogAliases_WhenCheckingQueryArguments()
    {
        var service = new DefaultToolInvocationValidationService(CreateCatalog());

        var result = service.Validate("web_search.Search", """{"query":"   "}""");

        result.IsValid.Should().BeFalse();
        result.UserMessage.Should().Be(AiOptimizationConstants.ToolValidationMessages.BlankQueryArgument);
    }

    /// <summary>
    /// 知识库工具短名应进入空查询参数校验。
    /// </summary>
    [Fact]
    public void Validate_ShouldUseCatalogAliases_ForKnowledgeBase()
    {
        var service = new DefaultToolInvocationValidationService(CreateCatalog());

        var result = service.Validate("knowledge-base.Search", """{"query":""}""");

        result.IsValid.Should().BeFalse();
        result.UserMessage.Should().Be(AiOptimizationConstants.ToolValidationMessages.BlankQueryArgument);
    }

    /// <summary>
    /// 带 Provider 包装前缀的工具名仍应进入目录解析。
    /// </summary>
    [Fact]
    public void Validate_ShouldSkipWrapperPrefix_WhenCheckingQueryArguments()
    {
        var service = new DefaultToolInvocationValidationService(CreateCatalog());

        var result = service.Validate("functions.web_search.Search", """{"query":" "}""");

        result.IsValid.Should().BeFalse();
        result.UserMessage.Should().Be(AiOptimizationConstants.ToolValidationMessages.BlankQueryArgument);
    }

    /// <summary>
    /// Provider 将工具调用名压成单段时，仍应先解析规范插件名再执行参数校验。
    /// </summary>
    [Theory]
    [InlineData("functions.WebSearchPlugin_Search")]
    [InlineData("tools.web-search-Search")]
    public void Validate_ShouldResolveSingleSegmentInvocationName_WhenCheckingQueryArguments(string toolName)
    {
        var service = new DefaultToolInvocationValidationService(CreateCatalog());

        var result = service.Validate(toolName, """{"query":" "}""");

        result.IsValid.Should().BeFalse();
        result.UserMessage.Should().Be(AiOptimizationConstants.ToolValidationMessages.BlankQueryArgument);
    }

    /// <summary>
    /// 宿主工具应继续执行文件路径参数校验。
    /// </summary>
    [Fact]
    public void Validate_ShouldCheckHostServiceFileArguments()
    {
        var service = new DefaultToolInvocationValidationService(CreateCatalog());

        var result = service.Validate("HostService.ReadFile", """{"path":" "}""");

        result.IsValid.Should().BeFalse();
        result.UserMessage.Should().Be(AiOptimizationConstants.ToolValidationMessages.BlankFileArgument);
    }

    /// <summary>
    /// 需要关键参数的受控工具不应放过空对象参数。
    /// </summary>
    [Theory]
    [InlineData("HostService.ExecuteCommandAsync")]
    [InlineData("CodeExecution.RunAsync")]
    [InlineData("web_search.Search")]
    [InlineData("knowledge-base.Search")]
    public void Validate_ShouldRejectEmptyObject_WhenToolRequiresArguments(string toolName)
    {
        var service = new DefaultToolInvocationValidationService(CreateCatalog());

        var result = service.Validate(toolName, "{}");

        result.IsValid.Should().BeFalse();
        result.UserMessage.Should().Be(AiOptimizationConstants.ToolValidationMessages.EmptyArguments);
    }

    /// <summary>
    /// 合法 JSON 但不是对象时不应进入工具执行层。
    /// </summary>
    [Theory]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("123")]
    public void Validate_ShouldRejectNonObjectJsonArguments(string argumentsJson)
    {
        var service = new DefaultToolInvocationValidationService(CreateCatalog());

        var result = service.Validate("HostService.ExecuteCommandAsync", argumentsJson);

        result.IsValid.Should().BeFalse();
        result.UserMessage.Should().Be(AiOptimizationConstants.ToolValidationMessages.NonObjectArguments);
    }

    /// <summary>
    /// 歧义工具名不应猜测为查询类工具。
    /// </summary>
    [Fact]
    public void Validate_ShouldNotGuess_WhenCatalogNameIsAmbiguous()
    {
        var service = new DefaultToolInvocationValidationService(CreateAmbiguousCatalog());

        var result = service.Validate("search.Run", """{"query":" "}""");

        result.IsValid.Should().BeTrue();
    }

    private static IToolCatalogService CreateCatalog()
    {
        return new StubToolCatalogService(
        [
            CreateTool(
                AiOptimizationConstants.ToolProtocol.WebSearchPlugin,
                AiOptimizationConstants.ToolExposureModes.Direct,
                aliases: ["WebSearch", "web_search", "web-search"]),
            CreateTool(
                AiOptimizationConstants.ToolProtocol.KnowledgeBasePlugin,
                AiOptimizationConstants.ToolExposureModes.Direct,
                aliases: ["KnowledgeBase", "knowledge_base", "knowledge-base"]),
            CreateTool(
                AiOptimizationConstants.ToolProtocol.HostServicePlugin,
                AiOptimizationConstants.ToolExposureModes.Deferred),
            CreateTool(
                AiOptimizationConstants.ToolProtocol.CodeExecutionPlugin,
                AiOptimizationConstants.ToolExposureModes.Deferred)
        ]);
    }

    private static IToolCatalogService CreateAmbiguousCatalog()
    {
        return new StubToolCatalogService(
        [
            CreateTool("Search", AiOptimizationConstants.ToolExposureModes.Direct),
            CreateTool(
                AiOptimizationConstants.ToolProtocol.WebSearchPlugin,
                AiOptimizationConstants.ToolExposureModes.Direct,
                displayName: "Search")
        ]);
    }

    private static ToolCatalogItemDto CreateTool(
        string pluginName,
        string exposureMode,
        string? displayName = null,
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
            Aliases = aliases ?? Array.Empty<string>()
        };
    }

    private sealed class StubToolCatalogService : IToolCatalogService
    {
        private readonly IReadOnlyList<ToolCatalogItemDto> _tools;

        public StubToolCatalogService(IReadOnlyList<ToolCatalogItemDto> tools)
        {
            _tools = tools;
        }

        public IReadOnlyList<ToolCatalogItemDto> GetAllTools()
        {
            return _tools;
        }

        public IReadOnlyList<ToolCatalogItemDto> GetDirectTools()
        {
            return _tools.DirectTools();
        }

        public IReadOnlyList<ToolCatalogItemDto> GetDeferredTools()
        {
            return _tools.DeferredTools();
        }

        public string? ResolvePluginName(string? requestedName)
        {
            return _tools.ResolvePluginName(requestedName);
        }

        public string ComputeSchemaHash()
        {
            return ToolSchemaFingerprint.ComputeHash(_tools);
        }
    }
}
