using DevNexus.Core.Abstractions;
using DevNexus.Core.Services.Tools;
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
            ExposureMode = AiOptimizationConstants.ToolExposureModes.Direct,
            ResultContract = AiOptimizationConstants.ToolResultContracts.WebSearch,
            SupportsParallelExecution = true,
            Aliases = ["WebSearch", "web_search", "web-search"]
        },
        new ToolCatalogItemDto
        {
            PluginName = AiOptimizationConstants.ToolProtocol.KnowledgeBasePlugin,
            DisplayName = AiOptimizationConstants.ToolDisplayNames.KnowledgeBase,
            Category = AiOptimizationConstants.ToolCategories.Knowledge,
            RiskLevel = AiOptimizationConstants.ToolRiskLevels.Low,
            ExposureMode = AiOptimizationConstants.ToolExposureModes.Direct,
            ResultContract = AiOptimizationConstants.ToolResultContracts.KnowledgeBase,
            SupportsParallelExecution = true,
            Aliases = ["KnowledgeBase", "knowledge_base", "knowledge-base"]
        },
        new ToolCatalogItemDto
        {
            PluginName = AiOptimizationConstants.ToolProtocol.HostServicePlugin,
            DisplayName = AiOptimizationConstants.ToolDisplayNames.HostService,
            Category = AiOptimizationConstants.ToolCategories.Coding,
            RiskLevel = AiOptimizationConstants.ToolRiskLevels.High,
            ExposureMode = AiOptimizationConstants.ToolExposureModes.Deferred,
            ResultContract = AiOptimizationConstants.ToolResultContracts.HostService,
            RequiresTaggedOutput = true
        },
        new ToolCatalogItemDto
        {
            PluginName = AiOptimizationConstants.ToolProtocol.CodeExecutionPlugin,
            DisplayName = AiOptimizationConstants.ToolDisplayNames.CodeExecution,
            Category = AiOptimizationConstants.ToolCategories.Coding,
            RiskLevel = AiOptimizationConstants.ToolRiskLevels.High,
            ExposureMode = AiOptimizationConstants.ToolExposureModes.Deferred,
            ResultContract = AiOptimizationConstants.ToolResultContracts.CodeExecution,
            RequiresTaggedOutput = true
        },
        new ToolCatalogItemDto
        {
            PluginName = AiOptimizationConstants.ToolProtocol.ImageGenerationPlugin,
            DisplayName = AiOptimizationConstants.ToolDisplayNames.ImageGeneration,
            Category = AiOptimizationConstants.ToolCategories.Creative,
            RiskLevel = AiOptimizationConstants.ToolRiskLevels.Medium,
            ExposureMode = AiOptimizationConstants.ToolExposureModes.Deferred,
            ResultContract = AiOptimizationConstants.ToolResultContracts.ImageGeneration
        }
    ];

    /// <inheritdoc />
    public IReadOnlyList<ToolCatalogItemDto> GetAllTools()
    {
        return Tools.OrderBy(tool => tool.PluginName, StringComparer.Ordinal).ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<ToolCatalogItemDto> GetDirectTools()
    {
        return Tools.DirectTools();
    }

    /// <inheritdoc />
    public IReadOnlyList<ToolCatalogItemDto> GetDeferredTools()
    {
        return Tools.DeferredTools();
    }

    /// <inheritdoc />
    public string? ResolvePluginName(string? requestedName)
    {
        return Tools.ResolvePluginName(requestedName);
    }

    /// <inheritdoc />
    public string ComputeSchemaHash()
    {
        return ToolSchemaFingerprint.ComputeHash(Tools);
    }
}
