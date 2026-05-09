using DevNexus.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Skills;

/// <summary>
/// Plugin 解析器实现 - 从 DI 容器按名称解析 Semantic Kernel Plugin
/// </summary>
public class PluginResolver : IPluginResolver
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PluginResolver> _logger;

    /// <summary>
    /// Plugin 名称 → 类型映射（用于动态解析）
    /// </summary>
    private readonly Dictionary<string, Type> _pluginTypeMap = new();

    public PluginResolver(IServiceProvider serviceProvider, ILogger<PluginResolver> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// 注册 Plugin 类型映射（DI 初始化时调用）
    /// </summary>
    /// <param name="pluginName">Plugin 名称</param>
    /// <param name="pluginType">Plugin 类型</param>
    public void RegisterPluginType(string pluginName, Type pluginType)
    {
        _pluginTypeMap[pluginName] = pluginType;
        _logger.LogDebug("[Skill.PluginResolver] 注册 Plugin 类型映射 | Name={Name} Type={Type}",
            pluginName, pluginType.Name);
    }

    /// <inheritdoc />
    public object? Resolve(string pluginName, Guid? sessionId = null, Guid? userId = null)
    {
        if (_pluginTypeMap.TryGetValue(pluginName, out var pluginType))
        {
            try
            {
                var plugin = _serviceProvider.GetService(pluginType);
                if (plugin != null)
                {
                    _logger.LogDebug("[Skill.PluginResolver] 解析 Plugin 成功 | Name={Name}", pluginName);
                    return plugin;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Skill.PluginResolver] 解析 Plugin 失败 | Name={Name}", pluginName);
            }
        }

        _logger.LogDebug("[Skill.PluginResolver] Plugin 未注册 | Name={Name}", pluginName);
        return null;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetAvailablePluginNames()
    {
        return _pluginTypeMap.Keys.ToList().AsReadOnly();
    }
}
