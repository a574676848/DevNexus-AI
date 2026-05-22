using DevNexus.Shared.DTOs;
using Microsoft.SemanticKernel;

namespace DevNexus.Core.Services.Tools;

/// <summary>
/// 工具调用并发策略。
/// </summary>
public static class ToolInvocationConcurrencyPolicy
{
    /// <summary>
    /// 根据当前已注册插件创建自动工具调用行为。
    /// </summary>
    public static FunctionChoiceBehavior CreateAutoFunctionChoiceBehavior(
        IEnumerable<ToolCatalogItemDto> catalogTools,
        IEnumerable<string> registeredPluginNames)
    {
        var allowParallelExecution = ShouldAllowParallelExecution(catalogTools, registeredPluginNames);

        return FunctionChoiceBehavior.Auto(options: new FunctionChoiceBehaviorOptions
        {
            AllowParallelCalls = allowParallelExecution,
            AllowConcurrentInvocation = allowParallelExecution
        });
    }

    /// <summary>
    /// 判断当前插件集合是否全部允许并行执行。
    /// </summary>
    public static bool ShouldAllowParallelExecution(
        IEnumerable<ToolCatalogItemDto> catalogTools,
        IEnumerable<string> registeredPluginNames)
    {
        var catalogByName = catalogTools
            .Where(tool => !string.IsNullOrWhiteSpace(tool.PluginName))
            .ToDictionary(tool => tool.PluginName, StringComparer.Ordinal);
        var pluginNames = registeredPluginNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (pluginNames.Count == 0)
        {
            return false;
        }

        foreach (var pluginName in pluginNames)
        {
            if (!catalogByName.TryGetValue(pluginName, out var tool))
            {
                return false;
            }

            if (!tool.SupportsParallelExecution)
            {
                return false;
            }
        }

        return true;
    }
}
