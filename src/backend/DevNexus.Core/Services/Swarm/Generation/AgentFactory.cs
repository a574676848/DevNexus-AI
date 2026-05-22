using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevNexus.Core.Abstractions;
using DevNexus.Domain.Models;
using DevNexus.Core.Services.Swarm.Analysis;
using DevNexus.Core.Services.Swarm.Context;
using DevNexus.Core.Services.Tools;
using DevNexus.Domain.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI; // For OpenAIPromptExecutionSettings

namespace DevNexus.Core.Services.Swarm.Generation;

/// <summary>
/// 智能体工厂 - 负责动态创建和配置智能体
/// </summary>
public class AgentFactory : IAgentFactory
{
    private readonly IAgentGenerator _agentGenerator;
    private readonly IKernelService _kernelService;
    private readonly IHostStructuredService _hostService;
    private readonly ISkillRegistry _skillRegistry;
    private readonly ISkillMatcher _skillMatcher;
    private readonly IConfirmationService _confirmationService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ICliRuntimeCoordinator? _cliRuntimeCoordinator;
    private readonly IUserContextAccessor? _userContextAccessor;
    private readonly IToolCatalogService? _toolCatalogService;

    public AgentFactory(
        IAgentGenerator agentGenerator,
        IKernelService kernelService,
        IHostStructuredService hostService,
        ISkillRegistry skillRegistry,
        ISkillMatcher skillMatcher,
        IConfirmationService confirmationService,
        ILoggerFactory loggerFactory,
        ICliRuntimeCoordinator? cliRuntimeCoordinator = null,
        IUserContextAccessor? userContextAccessor = null,
        IToolCatalogService? toolCatalogService = null)
    {
        _agentGenerator = agentGenerator;
        _kernelService = kernelService;
        _hostService = hostService;
        _skillRegistry = skillRegistry;
        _skillMatcher = skillMatcher;
        _confirmationService = confirmationService;
        _loggerFactory = loggerFactory;
        _cliRuntimeCoordinator = cliRuntimeCoordinator;
        _userContextAccessor = userContextAccessor;
        _toolCatalogService = toolCatalogService;
    }

    /// <inheritdoc />
    public async Task<ChatCompletionAgent> CreateAgentAsync(
        string taskDescription,
        DomainType domain,
        List<string> availableTools,
        Guid providerId,
        string sessionId,
        Core.Services.Swarm.Context.IBlackboard? blackboard = null,
        string? taskId = null,
        CancellationToken cancellationToken = default)
    {
        // 1. 生成 Agent Persona
        var persona = await _agentGenerator.GeneratePersonaAsync(taskDescription, domain, availableTools, providerId, cancellationToken);

        var logger = _loggerFactory.CreateLogger<AgentFactory>();
        logger.LogInformation("Agent generated: {Name} (Role: {Role})", persona.Name, persona.Role);

        // 2. 获取 Kernel 实例
        var kernel = await _kernelService.GetKernelAsync(providerId, cancellationToken);

        // 克隆 Kernel 以隔离每个 Agent 的插件环境
        var agentKernel = kernel.Clone();

        // **挂载安全拦截器**
        var interceptor = new Core.Services.Swarm.Safety.ConfirmationInterceptor(
            _confirmationService,
            _loggerFactory.CreateLogger<Core.Services.Swarm.Safety.ConfirmationInterceptor>(),
            sessionId);
        agentKernel.AutoFunctionInvocationFilters.Add(interceptor);

        // 3. 注入工具
        if (persona.Tools.Contains("HostService"))
        {
            // 如果已存在 HostService（可能来自基础 Kernel 的全局注册），先移除以允许覆盖
            if (agentKernel.Plugins.TryGetPlugin("HostService", out var existingPlugin))
            {
                agentKernel.Plugins.Remove(existingPlugin);
            }

            if (blackboard != null && !string.IsNullOrEmpty(taskId))
            {
                // 如果提供了黑板，则注入增强型的 SwarmHostPlugin
                var swarmPlugin = _cliRuntimeCoordinator == null || _userContextAccessor == null
                    ? new Context.SwarmHostPlugin(_hostService, blackboard, taskId)
                    : new Context.SwarmHostPlugin(
                        _hostService,
                        blackboard,
                        taskId,
                        _cliRuntimeCoordinator,
                        _userContextAccessor);
                agentKernel.Plugins.AddFromObject(swarmPlugin, "HostService");
                logger.LogInformation("Injected SwarmHostPlugin into agent {Name}", persona.Name);
            }
            else
            {
                // 否则注入文本适配插件，避免直接将服务接口对象暴露给模型。
                var hostPlugin = _cliRuntimeCoordinator == null || _userContextAccessor == null
                    ? new HostTextPlugin(_hostService)
                    : new HostTextPlugin(_hostService, _cliRuntimeCoordinator, _userContextAccessor);
                agentKernel.Plugins.AddFromObject(hostPlugin, "HostService");
                logger.LogInformation("Injected HostTextPlugin into agent {Name}", persona.Name);
            }
        }

        // 4. 注入 Skill 指令和 Plugin
        var enhancedInstructions = persona.Instructions;
        try
        {
            await _skillRegistry.InitializeAsync(cancellationToken);
            var availableSkills = _skillRegistry.GetAllEnabled();
            if (availableSkills.Count > 0)
            {
                var matchedSkills = await _skillMatcher.MatchAsync(
                    taskDescription, availableSkills, maxResults: 2, ct: cancellationToken);

                if (matchedSkills.Count > 0)
                {
                    var skillInstructions = new System.Text.StringBuilder();
                    skillInstructions.AppendLine("\n## 相关技能指令");

                    foreach (var match in matchedSkills)
                    {
                        var instruction = await _skillRegistry.LoadInstructionAsync(
                            match.Skill.Name, cancellationToken);
                        if (!string.IsNullOrWhiteSpace(instruction))
                        {
                            skillInstructions.AppendLine($"\n### {match.Skill.Name}");
                            skillInstructions.AppendLine(instruction);
                        }

                        // 注册 Skill 绑定的 Plugin
                        foreach (var pluginName in match.Skill.Plugins)
                        {
                            if (!agentKernel.Plugins.TryGetPlugin(pluginName, out _))
                            {
                                // HostService 已在上面处理，跳过
                                if (pluginName == "HostService") continue;
                                logger.LogDebug("[Swarm.Agent] Skill Plugin 待注册 | Plugin={Plugin}", pluginName);
                            }
                        }
                    }

                    enhancedInstructions = persona.Instructions + skillInstructions;
                    logger.LogInformation(
                        "[Swarm.Agent] Skill 指令已注入 | Agent={Name} MatchedSkills={Count}",
                        persona.Name, matchedSkills.Count);

                    // 审计日志：记录 Swarm Agent 场景的 Skill 匹配 (Topic=Skill.Audit)
                    foreach (var auditMatch in matchedSkills)
                    {
                        logger.LogInformation(
                            "[Skill.Audit] Skill 匹配 | Source=Swarm Agent={AgentName} " +
                            "SkillName={SkillName} Scope={Scope} Type={Type} " +
                            "Score={Score:F3} Method={Method}",
                            persona.Name,
                            auditMatch.Skill.Name, auditMatch.Skill.Scope, auditMatch.Skill.Type,
                            auditMatch.Score, auditMatch.Method);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Swarm.Agent] Skill 注入失败，使用原始指令 | Agent={Name}", persona.Name);
        }

        // 5. 创建 Agent
        var agent = new ChatCompletionAgent
        {
            Name = persona.Name,
            Instructions = enhancedInstructions,
            Kernel = agentKernel,
            Arguments = new KernelArguments(new OpenAIPromptExecutionSettings
            {
                 Temperature = persona.Temperature,
                 FunctionChoiceBehavior = CreateFunctionChoiceBehavior(agentKernel)
            })
        };

        return agent;
    }

    private FunctionChoiceBehavior CreateFunctionChoiceBehavior(Kernel agentKernel)
    {
        return ToolInvocationConcurrencyPolicy.CreateAutoFunctionChoiceBehavior(
            _toolCatalogService?.GetAllTools() ?? Array.Empty<DevNexus.Shared.DTOs.ToolCatalogItemDto>(),
            agentKernel.Plugins.Select(plugin => plugin.Name));
    }
}
