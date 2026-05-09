using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DevNexus.Core.Extensions;
using DevNexus.Core.Services.Swarm.Analysis;
using DevNexus.Domain.Abstractions;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace DevNexus.Core.Services.Swarm.Generation;

/// <summary>
/// 基于 LLM 动态生成智能体角色
/// </summary>
public class AgentGenerator : IAgentGenerator
{
    private readonly IKernelService _kernelService;
    private readonly ILogger<AgentGenerator> _logger;

    public AgentGenerator(IKernelService kernelService, ILogger<AgentGenerator> logger)
    {
        _kernelService = kernelService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AgentPersona> GeneratePersonaAsync(
        string taskDescription, 
        DomainType domain,
        List<string> availableTools,
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string systemPrompt = domain switch
            {
                DomainType.Coding => DevNexus.Shared.Constants.PromptConstants.Swarm.AgentGenCodingSystemPrompt,
                DomainType.OfficeWork => DevNexus.Shared.Constants.PromptConstants.Swarm.AgentGenOfficeSystemPrompt,
                DomainType.Creative => DevNexus.Shared.Constants.PromptConstants.Swarm.AgentGenCreativeSystemPrompt,
                _ => DevNexus.Shared.Constants.PromptConstants.Swarm.AgentGenGeneralSystemPrompt
            };

            systemPrompt += "\n\n请严格以 JSON 格式返回结果，禁止 Markdown。";
            
            var toolsList = string.Join(", ", availableTools);
            var userPrompt = string.Format(DevNexus.Shared.Constants.PromptConstants.Swarm.AgentGenUserPromptTemplate,
                taskDescription, domain.ToString(), toolsList);
            
            var history = new ChatHistory(systemPrompt);
            history.AddUserMessage(userPrompt);

            var result = await _kernelService.GetChatCompletionAsync(
                chatHistory: history,
                providerId: providerId,
                cancellationToken: cancellationToken,
                enableAutoFunctionCalling: false,
                auditScope: new ModelInvocationScopeDto
                {
                    OwnerType = ModelInvocationOwnerTypes.System,
                    SceneCode = ModelInvocationSceneCodes.GenerationAgentProfile,
                    SceneCategory = ModelInvocationSceneCategories.Swarm,
                    ResourceType = ModelInvocationResourceTypes.None
                });

            var rawContent = result.Content ?? "{}";
            var jsonContent = rawContent.CleanJsonContent();

            AgentPersona? persona = null;
            try
            {
                persona = JsonSerializer.Deserialize<AgentPersona>(jsonContent, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });
            }
            catch (JsonException jsonEx)
            {
                // 将 JSON 内容写入临时文件供调试
                var debugFilePath = SaveDebugJson(rawContent, jsonContent, jsonEx);
                
                _logger.LogError(jsonEx, 
                    "AgentPersona JSON 解析失败。调试文件已保存至：{DebugFilePath}。错误：{ErrorMessage}", 
                    debugFilePath, jsonEx.Message);
                    
                throw;
            }

            if (persona == null) throw new JsonException("Failed to generate persona - result is null.");

            // 再次校验工具列表，防止幻觉
            var validTools = persona.Tools.Where(t => availableTools.Contains(t)).ToList();
            
            return persona with { Tools = validTools };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "动态智能体角色生成失败，使用默认配置。");
            
            return new AgentPersona
            {
                Role = "GeneralAssistant",
                Name = "Assistant",
                Description = "A general purpose assistant.",
                Instructions = "You are a helpful AI assistant. Please help the user with their request.",
                Tools = new List<string>(),
                Temperature = 0.7
            };
        }
    }

    private static string SaveDebugJson(string rawContent, string cleanedContent, JsonException ex)
    {
        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var debugDir = Path.Combine(Path.GetTempPath(), "DevNexus_JsonDebug");
            Directory.CreateDirectory(debugDir);

            var fileName = $"agent_persona_error_{timestamp}.txt";
            var filePath = Path.Combine(debugDir, fileName);

            var debugContent = new System.Text.StringBuilder();
            debugContent.AppendLine("=== JSON 解析错误调试信息 (AgentPersona) ===");
            debugContent.AppendLine($"时间戳：{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            debugContent.AppendLine($"错误：{ex.Message}");
            debugContent.AppendLine();
            debugContent.AppendLine("=== 原始内容 ===");
            debugContent.AppendLine(rawContent);
            debugContent.AppendLine();
            debugContent.AppendLine("=== 清理后的内容 ===");
            debugContent.AppendLine(cleanedContent);
            debugContent.AppendLine();
            debugContent.AppendLine("=== 堆栈跟踪 ===");
            debugContent.AppendLine(ex.ToString());

            File.WriteAllText(filePath, debugContent.ToString());
            return filePath;
        }
        catch
        {
            return "Failed to save debug file";
        }
    }
}
