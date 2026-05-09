// using DevNexus.Domain.Abstractions via GlobalUsings
using DevNexus.Core.Abstractions;
using DevNexus.Core.Services.Swarm.Context;
using DevNexus.Infrastructure.Services.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using DevNexus.Domain.Models;

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
            // 注册 WebSearchPlugin
            if (!kernel.Plugins.TryGetPlugin("WebSearchPlugin", out _))
            {
                var webSearchPlugin = _serviceProvider.GetService<WebSearchPlugin>();
                if (webSearchPlugin != null)
                {
                    kernel.Plugins.AddFromObject(webSearchPlugin, "WebSearchPlugin");
                    _logger.LogDebug("[AI.Kernel] Registered WebSearchPlugin");
                }
            }

            // 注册笔记插件 (NotePlugin)
            if (!kernel.Plugins.TryGetPlugin("NotePlugin", out _))
            {
                var notePlugin = _serviceProvider.GetService<NotePlugin>();
                if (notePlugin != null)
                {
                    // 如果是会话级 Kernel，注入上下文（重要！）
                    if (sessionId.HasValue && userId.HasValue)
                    {
                        notePlugin.SetContext(sessionId.Value, userId.Value);
                        _logger.LogDebug("[AI.Kernel] Initialized NotePlugin context SessionId={SessionId} UserId={UserId}",
                            sessionId, userId);
                    }

                    kernel.Plugins.AddFromObject(notePlugin, "NotePlugin");
                    _logger.LogDebug("[AI.Kernel] Registered NotePlugin");
                }
            }

            // 注册文生图插件 (ImageGenerationPlugin)
            if (!kernel.Plugins.TryGetPlugin("ImageGeneration", out _))
            {
                var imageGenerationPlugin = _serviceProvider.GetService<ImageGenerationPlugin>();
                if (imageGenerationPlugin != null)
                {
                    // 如果是会话级 Kernel，注入上下文
                    if (sessionId.HasValue && userId.HasValue)
                    {
                        imageGenerationPlugin.SetContext(sessionId.Value, userId.Value);
                        _logger.LogDebug("[AI.Kernel] Initialized ImageGenerationPlugin context SessionId={SessionId}", sessionId);
                    }

                    kernel.Plugins.AddFromObject(imageGenerationPlugin, "ImageGeneration");
                    _logger.LogDebug("[AI.Kernel] Registered ImageGenerationPlugin");
                }
            }

            // 注册代码执行插件 (CodeExecutionPlugin - Phase 3)
            if (!kernel.Plugins.TryGetPlugin("CodeExecution", out _))
            {
                var codeExecPlugin = _serviceProvider.GetService<CodeExecutionPlugin>();
                if (codeExecPlugin != null)
                {
                    kernel.Plugins.AddFromObject(codeExecPlugin, "CodeExecution");
                    _logger.LogDebug("[AI.Kernel] Registered CodeExecutionPlugin");
                }
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
            if (!kernel.Plugins.TryGetPlugin("KnowledgeBasePlugin", out _))
            {
                var kbService = _serviceProvider.GetRequiredService<IKnowledgeBaseService>();
                var kbLogger = _serviceProvider.GetRequiredService<ILogger<KnowledgeBasePlugin>>();

                var kbPlugin = new KnowledgeBasePlugin(kbService, kbLogger, userId);
                kernel.Plugins.AddFromObject(kbPlugin, "KnowledgeBasePlugin");

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
            if (kernel.Plugins.TryGetPlugin("HostService", out _))
                return;

            var hostService = _serviceProvider.GetService<IHostStructuredService>();
            if (hostService != null)
            {
                kernel.Plugins.AddFromObject(new HostTextPlugin(hostService), "HostService");
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
}
