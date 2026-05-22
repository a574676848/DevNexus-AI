using DevNexus.Core.Services.Tools;
using DevNexus.Shared.DTOs;
using FluentAssertions;
using Microsoft.SemanticKernel;
using Xunit;

namespace DevNexus.Core.Tests.Services.Tools;

public sealed class ToolInvocationConcurrencyPolicyTests
{
    [Fact]
    public void ShouldAllowParallelExecution_ShouldReturnTrue_WhenAllRegisteredToolsSupportParallelExecution()
    {
        var result = ToolInvocationConcurrencyPolicy.ShouldAllowParallelExecution(
            CreateCatalog(),
            ["WebSearchPlugin", "KnowledgeBasePlugin"]);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldAllowParallelExecution_ShouldReturnFalse_WhenStatefulToolIsRegistered()
    {
        var result = ToolInvocationConcurrencyPolicy.ShouldAllowParallelExecution(
            CreateCatalog(),
            ["WebSearchPlugin", "HostService"]);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldAllowParallelExecution_ShouldReturnFalse_WhenPluginIsUnknown()
    {
        var result = ToolInvocationConcurrencyPolicy.ShouldAllowParallelExecution(
            CreateCatalog(),
            ["CustomPlugin"]);

        result.Should().BeFalse();
    }

    [Fact]
    public void CreateAutoFunctionChoiceBehavior_ShouldDisableParallelInvocation_WhenStatefulToolExists()
    {
        var behavior = ToolInvocationConcurrencyPolicy.CreateAutoFunctionChoiceBehavior(
            CreateCatalog(),
            ["HostService"]);

        var options = ((AutoFunctionChoiceBehavior)behavior).Options;
        options.Should().NotBeNull();
        options.AllowParallelCalls.Should().BeFalse();
        options.AllowConcurrentInvocation.Should().BeFalse();
    }

    private static IReadOnlyList<ToolCatalogItemDto> CreateCatalog()
    {
        return
        [
            new ToolCatalogItemDto
            {
                PluginName = "WebSearchPlugin",
                DisplayName = "网络检索",
                Category = "Research",
                RiskLevel = "Low",
                ExposureMode = "Direct",
                ResultContract = "返回网络检索结果。",
                SupportsParallelExecution = true
            },
            new ToolCatalogItemDto
            {
                PluginName = "KnowledgeBasePlugin",
                DisplayName = "知识库检索",
                Category = "Knowledge",
                RiskLevel = "Low",
                ExposureMode = "Direct",
                ResultContract = "返回知识库检索结果。",
                SupportsParallelExecution = true
            },
            new ToolCatalogItemDto
            {
                PluginName = "HostService",
                DisplayName = "宿主文件与命令",
                Category = "Coding",
                RiskLevel = "High",
                ExposureMode = "Deferred",
                ResultContract = "返回宿主命令执行结果。"
            }
        ];
    }
}
