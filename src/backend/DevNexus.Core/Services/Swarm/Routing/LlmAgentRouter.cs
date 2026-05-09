using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DevNexus.Core.Extensions;
using DevNexus.Core.Services.Swarm.Generation;
using DevNexus.Domain.Abstractions;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace DevNexus.Core.Services.Swarm.Routing;

/// <summary>
/// 基于 LLM 的动态路由实现
/// 使用推理逻辑对 Agent 的能力进行评分和匹配
/// </summary>
public class LlmAgentRouter : IAgentRouter
{
    private readonly IKernelService _kernelService;
    private readonly ILogger<LlmAgentRouter> _logger;

    public LlmAgentRouter(IKernelService kernelService, ILogger<LlmAgentRouter> logger)
    {
        _kernelService = kernelService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AgentPersona?> RouteRequestAsync(
        string taskDescription,
        List<AgentPersona> candidates,
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        if (candidates == null || candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0];

        _logger.LogInformation("正在进行 LLM 动态路由，候选 Agent 数量: {Count}", candidates.Count);

        var topK = await MatchTopKAsync(taskDescription, candidates, providerId, 3, cancellationToken);
        return topK.FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<List<AgentPersona>> MatchTopKAsync(
        string taskDescription,
        List<AgentPersona> candidates,
        Guid providerId,
        int k = 3,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var agentList = string.Join("\n", candidates.Select(c => $"- ID: {c.Name} | Role: {c.Role} | Description: {c.Description}"));
            var prompt = string.Format(DevNexus.Shared.Constants.PromptConstants.Swarm.TeamAssemblerPrompt, taskDescription, agentList);
            
            var history = new ChatHistory();
            history.AddUserMessage(prompt);

            var response = await _kernelService.GetChatCompletionAsync(
                history,
                providerId,
                cancellationToken: cancellationToken,
                enableAutoFunctionCalling: false,
                auditScope: new ModelInvocationScopeDto
                {
                    OwnerType = ModelInvocationOwnerTypes.System,
                    SceneCode = ModelInvocationSceneCodes.RoutingAgentSelect,
                    SceneCategory = ModelInvocationSceneCategories.Swarm,
                    ResourceType = ModelInvocationResourceTypes.None
                });
            var rawContent = response.Content ?? "{}";
            var json = rawContent.CleanJsonContent();
            
            _logger.LogDebug("Agent routing JSON preview: {JsonPreview}", 
                json.Length > 150 ? json.Substring(0, 150) + "..." : json);

            var result = JsonSerializer.Deserialize<RoutingResult>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            var matched = new List<AgentPersona>();
            if (result?.SelectedAgents != null)
            {
                foreach (var selected in result.SelectedAgents.Take(k))
                {
                    var found = candidates.FirstOrDefault(c => c.Name == selected.AgentId);
                    if (found != null) matched.Add(found);
                }
            }

            return matched.Count > 0 ? matched : candidates.Take(k).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "路由匹配执行失败，降级回默认排序");
            return candidates.Take(k).ToList();
        }
    }

    private class RoutingResult
    {
        public string IntentAnalysis { get; set; } = string.Empty;
        public List<string> RequiredCapabilities { get; set; } = new();
        public List<SelectedAgent> SelectedAgents { get; set; } = new();
    }

    private class SelectedAgent
    {
        public string AgentId { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}
