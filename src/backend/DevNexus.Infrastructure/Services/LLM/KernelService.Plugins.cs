// using DevNexus.Domain.Abstractions via GlobalUsings
using DevNexus.Core.Abstractions;
using DevNexus.Core.Services.Swarm.Context;
using DevNexus.Infrastructure.Services.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using DevNexus.Domain.Models;
using DevNexus.Shared.Constants;

namespace DevNexus.Infrastructure.Services.LLM;

public partial class KernelService
{
    /// <summary>
    /// 注册所有 Semantic Kernel 插件
    /// </summary>
    private void RegisterPlugins(Kernel kernel, Guid? sessionId = null, Guid? userId = null)
    {
        try
        {
            foreach (var tool in _toolCatalogService.GetCoreTools())
            {
                RegisterCatalogPlugin(kernel, tool.PluginName, sessionId, userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AI.Kernel] Error registering plugins");
        }
    }

    /// <summary>
    /// 动态注册 KnowledgeBasePlugin（需要 UserId）
    /// </summary>
    /// <param name="kernel">Kernel 实例</param>
    /// <param name="userId">用户 ID</param>
    private void RegisterKnowledgeBasePlugin(Kernel kernel, Guid userId)
    {
        try
        {
            if (!kernel.Plugins.TryGetPlugin(AiOptimizationConstants.ToolProtocol.KnowledgeBasePlugin, out _))
            {
                var kbService = _serviceProvider.GetRequiredService<IKnowledgeBaseService>();
                var kbLogger = _serviceProvider.GetRequiredService<ILogger<KnowledgeBasePlugin>>();

                var kbPlugin = new KnowledgeBasePlugin(kbService, kbLogger, userId);
                kernel.Plugins.AddFromObject(kbPlugin, AiOptimizationConstants.ToolProtocol.KnowledgeBasePlugin);

                _logger.LogDebug("[AI.Kernel] Registered KnowledgeBasePlugin for UserId={UserId}", userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AI.Kernel] Error registering KnowledgeBasePlugin for UserId={UserId}", userId);
        }
    }

    /// <summary>
    /// 注册 HostService 作为全局 Plugin（使 LLM 可在 Skill 指导下调用文件/命令操作）
    /// 非 Swarm 模式下通过 GlobalHostServiceInterceptor 进行安全拦截
    /// </summary>
    /// <param name="kernel">Kernel 实例</param>
    private void RegisterHostServicePlugin(Kernel kernel)
    {
        try
        {
            // 避免重复注册
            if (kernel.Plugins.TryGetPlugin(AiOptimizationConstants.ToolProtocol.HostServicePlugin, out _))
                return;

            var hostService = _serviceProvider.GetService<IHostStructuredService>();
            if (hostService != null)
            {
                kernel.Plugins.AddFromObject(new HostTextPlugin(hostService), AiOptimizationConstants.ToolProtocol.HostServicePlugin);
                _logger.LogDebug("[AI.Kernel] 已注册 HostService 全局 Plugin");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AI.Kernel] 注册 HostService Plugin 失败");
        }
    }

    /// <summary>
    /// 注册 Skill 绑定的 Plugin（仅注册匹配到的 Skill 所需的 Plugin）
    /// </summary>
    /// <param name="kernel">Kernel 实例</param>
    /// <param name="matchedSkills">匹配到的 Skill 列表</param>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="userId">用户 ID</param>
    private void RegisterSkillPlugins(
        Kernel kernel,
        IEnumerable<SkillMatchResult>? matchedSkills,
        Guid? sessionId = null,
        Guid? userId = null)
    {
        if (matchedSkills == null) return;

        var pluginResolver = _serviceProvider.GetService<Core.Abstractions.IPluginResolver>();
        if (pluginResolver == null)
        {
            _logger.LogDebug("[AI.Kernel] IPluginResolver 未注册，跳过 Skill Plugin 注册");
            return;
        }

        foreach (var match in matchedSkills)
        {
            foreach (var pluginName in match.Skill.Plugins)
            {
                if (RegisterCatalogPlugin(kernel, pluginName, sessionId, userId))
                {
                    _logger.LogDebug("[AI.Kernel] 注册 Skill 目录 Plugin | Skill={Skill} Plugin={Plugin}",
                        match.Skill.Name, pluginName);
                    continue;
                }

                // 避免重复注册
                if (kernel.Plugins.TryGetPlugin(pluginName, out _))
                    continue;

                var plugin = pluginResolver.Resolve(pluginName, sessionId, userId);
                if (plugin != null)
                {
                    kernel.Plugins.AddFromObject(plugin, pluginName);
                    _logger.LogDebug("[AI.Kernel] 注册 Skill Plugin | Skill={Skill} Plugin={Plugin}",
                        match.Skill.Name, pluginName);
                }
                else
                {
                    _logger.LogWarning("[AI.Kernel] Skill 引用的 Plugin 未找到 | Skill={Skill} Plugin={Plugin}",
                        match.Skill.Name, pluginName);
                }
            }
        }
    }

    private bool RegisterCatalogPlugin(
        Kernel kernel,
        string pluginName,
        Guid? sessionId,
        Guid? userId)
    {
        if (kernel.Plugins.TryGetPlugin(pluginName, out _))
        {
            return true;
        }

        switch (pluginName)
        {
            case AiOptimizationConstants.ToolProtocol.WebSearchPlugin:
                return AddPluginFromService<WebSearchPlugin>(kernel, pluginName);
            case AiOptimizationConstants.ToolProtocol.ImageGenerationPlugin:
                var imageGenerationPlugin = _serviceProvider.GetService<ImageGenerationPlugin>();
                if (imageGenerationPlugin == null)
                {
                    return false;
                }

                if (sessionId.HasValue && userId.HasValue)
                {
                    imageGenerationPlugin.SetContext(sessionId.Value, userId.Value);
                    _logger.LogDebug("[AI.Kernel] Initialized ImageGenerationPlugin context SessionId={SessionId}", sessionId);
                }

                kernel.Plugins.AddFromObject(imageGenerationPlugin, pluginName);
                return true;
            case AiOptimizationConstants.ToolProtocol.CodeExecutionPlugin:
                return AddPluginFromService<CodeExecutionPlugin>(kernel, pluginName);
            case AiOptimizationConstants.ToolProtocol.HostServicePlugin:
                RegisterHostServicePlugin(kernel);
                return kernel.Plugins.TryGetPlugin(pluginName, out _);
            default:
                return false;
        }
    }

    private bool AddPluginFromService<TPlugin>(Kernel kernel, string pluginName)
        where TPlugin : class
    {
        var plugin = _serviceProvider.GetService<TPlugin>();
        if (plugin == null)
        {
            return false;
        }

        kernel.Plugins.AddFromObject(plugin, pluginName);
        _logger.LogDebug("[AI.Kernel] Registered {PluginName}", pluginName);
        return true;
    }
}
