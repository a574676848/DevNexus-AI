using System.Text.Json;
using DevNexus.Core.Abstractions;
using DevNexus.Core.Services.Chat;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;

namespace DevNexus.Infrastructure.Services.Tools;

/// <summary>
/// Infrastructure 层工具目录服务。
/// </summary>
public sealed class InfrastructureToolCatalogService : IToolCatalogService
{
    private static readonly IReadOnlyList<ToolCatalogItemDto> Tools =
    [
        new ToolCatalogItemDto
        {
            PluginName = AiOptimizationConstants.ToolProtocol.WebSearchPlugin,
            DisplayName = AiOptimizationConstants.ToolDisplayNames.WebSearch,
            Category = AiOptimizationConstants.ToolCategories.Research,
            RiskLevel = AiOptimizationConstants.ToolRiskLevels.Low,
            IsCore = true,
            ResultContract = AiOptimizationConstants.ToolResultContracts.WebSearch
        },
        new ToolCatalogItemDto
        {
            PluginName = AiOptimizationConstants.ToolProtocol.KnowledgeBasePlugin,
            DisplayName = AiOptimizationConstants.ToolDisplayNames.KnowledgeBase,
            Category = AiOptimizationConstants.ToolCategories.Knowledge,
            RiskLevel = AiOptimizationConstants.ToolRiskLevels.Low,
            IsCore = true,
            ResultContract = AiOptimizationConstants.ToolResultContracts.KnowledgeBase
        },
        new ToolCatalogItemDto
        {
            PluginName = AiOptimizationConstants.ToolProtocol.HostServicePlugin,
            DisplayName = AiOptimizationConstants.ToolDisplayNames.HostService,
            Category = AiOptimizationConstants.ToolCategories.Coding,
            RiskLevel = AiOptimizationConstants.ToolRiskLevels.High,
            IsCore = false,
            ResultContract = AiOptimizationConstants.ToolResultContracts.HostService
        },
        new ToolCatalogItemDto
        {
            PluginName = AiOptimizationConstants.ToolProtocol.CodeExecutionPlugin,
            DisplayName = AiOptimizationConstants.ToolDisplayNames.CodeExecution,
            Category = AiOptimizationConstants.ToolCategories.Coding,
            RiskLevel = AiOptimizationConstants.ToolRiskLevels.High,
            IsCore = false,
            ResultContract = AiOptimizationConstants.ToolResultContracts.CodeExecution
        },
        new ToolCatalogItemDto
        {
            PluginName = AiOptimizationConstants.ToolProtocol.ImageGenerationPlugin,
            DisplayName = AiOptimizationConstants.ToolDisplayNames.ImageGeneration,
            Category = AiOptimizationConstants.ToolCategories.Creative,
            RiskLevel = AiOptimizationConstants.ToolRiskLevels.Medium,
            IsCore = false,
            ResultContract = AiOptimizationConstants.ToolResultContracts.ImageGeneration
        }
    ];

    /// <inheritdoc />
    public IReadOnlyList<ToolCatalogItemDto> GetCoreTools()
    {
        return Tools.Where(tool => tool.IsCore).OrderBy(tool => tool.PluginName, StringComparer.Ordinal).ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<ToolCatalogItemDto> GetDomainTools()
    {
        return Tools.Where(tool => !tool.IsCore).OrderBy(tool => tool.PluginName, StringComparer.Ordinal).ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<ToolCatalogItemDto> GetAllTools()
    {
        return Tools.OrderBy(tool => tool.PluginName, StringComparer.Ordinal).ToList();
    }

    /// <inheritdoc />
    public string ComputeSchemaHash()
    {
        var json = JsonSerializer.Serialize(GetAllTools());
        return PromptFingerprint.ComputeHash(json);
    }
}
