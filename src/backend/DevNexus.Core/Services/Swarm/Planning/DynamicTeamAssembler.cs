using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DevNexus.Core.Extensions;
using DevNexus.Core.Services.Swarm.Analysis;
using DevNexus.Domain.Abstractions;
using DevNexus.Core.Services.Swarm.Generation;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Services.Swarm.Planning;

/// <summary>
/// 动态团队组建器 - 根据任务描述自动计算并生成最适合的 Agent 团队配置
/// </summary>
public class DynamicTeamAssembler
{
    private readonly IKernelService _kernelService;
    private readonly ILogger<DynamicTeamAssembler> _logger;

    public DynamicTeamAssembler(IKernelService kernelService, ILogger<DynamicTeamAssembler> logger)
    {
        _kernelService = kernelService;
        _logger = logger;
    }

    /// <summary>
    /// 根据任务描述建议一组智能体角色
    /// </summary>
    public async Task<List<AgentPersona>> AssembleTeamAsync(
        string description, 
        DomainType domain, 
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Assembling dynamic team for domain {Domain}...", domain);

            var history = new ChatHistory();
            history.AddSystemMessage("You are a Swarm Team Architect. Based on the task description, recommend 1 to 3 distinct agent roles that are best suited to collaborate on this task. Return a JSON array of objects with 'Name', 'Role', 'Description', and 'Instructions'.");
            
            var userPrompt = $"Task Description: {description}\nDomain: {domain}\n\nRecommend the team structure in JSON format:";
            history.AddUserMessage(userPrompt);

            var response = await _kernelService.GetChatCompletionAsync(
                history,
                providerId,
                cancellationToken: cancellationToken,
                enableAutoFunctionCalling: false,
                auditScope: new ModelInvocationScopeDto
                {
                    OwnerType = ModelInvocationOwnerTypes.System,
                    SceneCode = ModelInvocationSceneCodes.GenerationAgentProfile,
                    SceneCategory = ModelInvocationSceneCategories.Swarm,
                    ResourceType = ModelInvocationResourceTypes.None
                });

            var rawContent = response.Content ?? "[]";
            var json = rawContent.CleanJsonContent();
            
            _logger.LogDebug("Dynamic team assembly JSON preview: {JsonPreview}", 
                json.Length > 150 ? json.Substring(0, 150) + "..." : json);
            
            var result = JsonSerializer.Deserialize<TeamRecommendation>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            return result?.Agents ?? new List<AgentPersona>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to assemble team dynamically. Falling back to default singleton agent.");
            return new List<AgentPersona>();
        }
    }

    private class TeamRecommendation
    {
        public List<AgentPersona> Agents { get; set; } = new();
    }
}
