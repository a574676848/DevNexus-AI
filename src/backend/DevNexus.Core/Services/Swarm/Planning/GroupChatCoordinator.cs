using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DevNexus.Core.Services.Swarm.Analysis;
using DevNexus.Core.Services.Swarm.Generation;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Models.Swarm;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using DevNexus.Core.Extensions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Services.Swarm.Planning;

/// <summary>
/// 处理单个上下文工作包下的多智能体群聊讨论
/// </summary>
public class GroupChatCoordinator
{
    private readonly IAgentFactory _agentFactory;
    private readonly IKernelService _kernelService;
    private readonly ISwarmEventService _eventService;
    private readonly ILogger<GroupChatCoordinator> _logger;
    
    // 可配置的讨论轮数 (#7)
    public int MaxRounds { get; set; } = 2;

    public GroupChatCoordinator(
        IAgentFactory agentFactory,
        IKernelService kernelService,
        ISwarmEventService eventService,
        ILogger<GroupChatCoordinator> logger)
    {
        _agentFactory = agentFactory;
        _kernelService = kernelService;
        _eventService = eventService;
        _logger = logger;
    }

    public async Task<string> RunGroupChatAsync(
        ContextWorkPackage package,
        DomainType domain,
        List<AgentPersona> team,
        Guid providerId,
        string sessionId,
        string context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("启动群聊讨论 — 任务 {TaskId} — 参与智能体数：{Count} — 目标轮数：{Rounds}",
            package.Id, team.Count, MaxRounds);

        return await _kernelService.RunWithAuditScopeAsync(
            new ModelInvocationScopeDto
            {
                OwnerType = ModelInvocationOwnerTypes.System,
                SceneCode = ModelInvocationSceneCodes.SwarmWorkPackageGroupChatRound,
                SceneCategory = ModelInvocationSceneCategories.Swarm,
                ResourceType = ModelInvocationResourceTypes.ContextWorkPackageRecord,
                ResourceId = package.Id
            },
            async () =>
            {
                var transcript = new StringBuilder();
                transcript.AppendLine($"--- 任务群聊讨论：{package.Title} ---");
                transcript.AppendLine($"上下文：{context}");
                transcript.AppendLine();

                // 1. 预创建并缓存所有参会智能体，避免每轮重复创建 (#7)
                var agentCache = new Dictionary<string, ChatCompletionAgent>();
                foreach (var persona in team)
                {
                    var agent = await _agentFactory.CreateAgentAsync(
                        persona.Description,
                        domain,
                        new List<string>(),
                        providerId,
                        "group-chat-" + package.Id,
                        null,
                        package.Id,
                        cancellationToken);
                    agentCache[persona.Name] = agent;

                    _ = _eventService.NotifyAgentStatusChangedAsync(sessionId, persona.Name, "待命", "等待发言...", cancellationToken);
                }

                for (int round = 1; round <= MaxRounds; round++)
                {
                    foreach (var persona in team)
                    {
                        if (!agentCache.TryGetValue(persona.Name, out var agent)) continue;

                        _ = _eventService.NotifyAgentStatusChangedAsync(sessionId, persona.Name, "发言", $"第 {round} 轮...", cancellationToken);
                
                        var chatService = agent.Kernel.GetRequiredService<IChatCompletionService>();
                        var history = new ChatHistory(agent.Instructions ?? string.Empty);
                        history.AddUserMessage($"{transcript}\n\n{string.Format(PromptConstants.Swarm.GroupChatUserPromptTemplate, round, package.Title)}");

                        var response = await chatService.GetChatMessageContentAsync(history, null, agent.Kernel, cancellationToken);
                        var content = response.Content ?? string.Empty;

                        transcript.AppendLine($"**[{persona.Role}: {persona.Name}] (Round {round})**:");
                        transcript.AppendLine(content);
                        transcript.AppendLine();
                
                        _ = _eventService.NotifyAgentStatusChangedAsync(sessionId, persona.Name, "待命", "聆听其他成员", cancellationToken);
                    }
                }

                var moderatorPersona = team.First();
                if (agentCache.TryGetValue(moderatorPersona.Name, out var moderatorAgent))
                {
                    await _kernelService.RunWithAuditScopeAsync(
                        new ModelInvocationScopeDto
                        {
                            OwnerType = ModelInvocationOwnerTypes.System,
                            SceneCode = ModelInvocationSceneCodes.SwarmWorkPackageGroupChatSummary,
                            SceneCategory = ModelInvocationSceneCategories.Swarm,
                            ResourceType = ModelInvocationResourceTypes.ContextWorkPackageRecord,
                            ResourceId = package.Id
                        },
                        async () =>
                        {
                            var modChatService = moderatorAgent.Kernel.GetRequiredService<IChatCompletionService>();
                            var modHistory = new ChatHistory(PromptConstants.Swarm.GroupChatModeratorSystemPrompt);
                            modHistory.AddUserMessage(transcript.ToString());

                            var resultStr = await modChatService.GetAutoContinuedChatMessageContentAsync(
                                modHistory, 
                                null, 
                                moderatorAgent.Kernel, 
                                _logger, 
                                $"GroupChatSummary-{package.Id}", 
                                maxContinuations: 10, 
                                cancellationToken);
            
                            _ = _eventService.NotifyAgentStatusChangedAsync(sessionId, moderatorPersona.Name, "完成", "讨论汇总完成。", cancellationToken);

                            if (string.IsNullOrWhiteSpace(resultStr))
                            {
                                resultStr = "群聊汇总未能产生有效结果。";
                            }

                            transcript.Clear();
                            transcript.Append(resultStr);
                        });

                    return transcript.ToString();
                }
        
                return "群聊由于智能体初始化失败而无法生成结果。";
            });
    }
}
