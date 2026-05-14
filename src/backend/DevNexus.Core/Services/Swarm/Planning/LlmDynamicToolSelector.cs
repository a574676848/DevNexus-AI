using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DevNexus.Core.Abstractions;
using DevNexus.Core.Extensions;
using DevNexus.Core.Services.Swarm.Analysis;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace DevNexus.Core.Services.Swarm.Planning;

/// <summary>
/// 基于 LLM 的动态工具选择器
/// 根据任务语义智能推荐合适的工具子集
/// </summary>
public class LlmDynamicToolSelector : IDynamicToolSelector
{
    private readonly IKernelService _kernelService;
    private readonly ILogger<LlmDynamicToolSelector> _logger;

    // 工具类别映射（启发式分类）
    private static readonly Dictionary<string, List<string>> ToolCategories = new()
    {
        ["文件操作"] = new() { "ReadFile", "WriteFile", "ListFiles", "DeleteFile" },
        ["代码分析"] = new() { "CodeAnalysis", "GetCodeStructure", "FindReferences" },
        ["网络请求"] = new() { "WebSearch", "HttpRequest", "DownloadFile" },
        ["数据处理"] = new() { "ParseJson", "ParseXml", "TransformData" },
        ["版本控制"] = new() { "GitCommit", "GitPush", "GitPull", "GitStatus" },
        ["图像生成"] = new() { "GenerateImage", "ImageAnalysis" }
    };

    public LlmDynamicToolSelector(
        IKernelService kernelService,
        ILogger<LlmDynamicToolSelector> logger)
    {
        _kernelService = kernelService;
        _logger = logger;
    }

    public async Task<List<string>> SelectToolsAsync(
        string taskDescription, 
        List<string> availableTools, 
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskDescription))
        {
            _logger.LogWarning("任务描述为空，返回所有可用工具");
            return availableTools;
        }

        if (availableTools.Count == 0)
        {
            _logger.LogWarning("可用工具列表为空");
            return new List<string>();
        }

        // 1. 先尝试启发式匹配（快速路径）
        var heuristicTools = HeuristicSelection(taskDescription, availableTools);
        if (heuristicTools.Count > 0 && heuristicTools.Count <= 5)
        {
            _logger.LogDebug("启发式选择工具成功: {Tools}", string.Join(", ", heuristicTools));
            return heuristicTools;
        }

        try
        {
            var selectedTools = await LlmSelectionAsync(taskDescription, availableTools, providerId, cancellationToken);
            _logger.LogInformation("LLM 选择工具: {Tools}", string.Join(", ", selectedTools));
            return selectedTools;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM 工具选择失败，回退到启发式结果");
            return heuristicTools.Count > 0 ? heuristicTools : availableTools.Take(5).ToList();
        }
    }

    public async Task<List<string>> PredictToolsAsync(
        string taskType, 
        DomainType domain, 
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        // 基于任务类型和领域的预测逻辑
        var predictedCategories = new List<string>();

        // 根据 domain 映射工具类别
        switch (domain)
        {
            case DomainType.Coding:
                predictedCategories.AddRange(new[] { "代码分析", "文件操作", "版本控制", "网络请求" });
                break;
            case DomainType.DataAnalysis:
                predictedCategories.AddRange(new[] { "数据处理", "代码分析" });
                break;
            case DomainType.Creative:
                predictedCategories.AddRange(new[] { "图像生成", "文件操作" });
                break;
            case DomainType.OfficeWork:
                predictedCategories.Add("文件操作");
                break;
            default:
                predictedCategories.AddRange(new[] { "文件操作", "网络请求" });
                break;
        }

        // 根据任务类型追加工具
        if (taskType.Contains("搜索") || taskType.Contains("查询"))
        {
            predictedCategories.Add("网络请求");
        }
        if (taskType.Contains("图片") || taskType.Contains("图像") || taskType.Contains("UI"))
        {
            predictedCategories.Add("图像生成");
        }
        if (taskType.Contains("文档"))
        {
            predictedCategories.Add("文件操作");
        }

        // 合并所有预测的工具
        var predictedTools = predictedCategories
            .SelectMany(category => ToolCategories.GetValueOrDefault(category, new List<string>()))
            .Distinct()
            .ToList();

        _logger.LogDebug("基于任务类型 '{TaskType}' 和领域 '{Domain}' 预测工具: {Tools}", 
            taskType, domain, string.Join(", ", predictedTools));

        return await Task.FromResult(predictedTools);
    }

    /// <summary>
    /// 启发式工具选择（基于关键词匹配）
    /// </summary>
    private List<string> HeuristicSelection(string taskDescription, List<string> availableTools)
    {
        var selected = new HashSet<string>();
        var lowerDesc = taskDescription.ToLowerInvariant();

        // 关键词映射
        var keywordMap = new Dictionary<string, string[]>
        {
            ["ReadFile"] = new[] { "读取", "查看", "获取文件", "read file" },
            ["WriteFile"] = new[] { "写入", "保存", "创建文件", "write file", "修改文件" },
            ["ListFiles"] = new[] { "列出", "查找文件", "list files", "遍历" },
            ["WebSearch"] = new[] { "搜索", "查询", "web search", "google", "bing" },
            ["GenerateImage"] = new[] { "生成图片", "图像生成", "generate image", "dall-e" },
            ["GitCommit"] = new[] { "提交", "commit", "git提交" }
        };

        foreach (var tool in availableTools)
        {
            if (keywordMap.TryGetValue(tool, out var keywords))
            {
                if (keywords.Any(kw => lowerDesc.Contains(kw)))
                {
                    selected.Add(tool);
                }
            }
        }

        return selected.ToList();
    }

    /// <summary>
    /// 使用 LLM 进行工具选择
    /// </summary>
    private async Task<List<string>> LlmSelectionAsync(
        string taskDescription, 
        List<string> availableTools, 
        Guid providerId,
        CancellationToken cancellationToken)
    {
        var systemPrompt = string.Format(
            PromptConstants.Tooling.ToolDecisionPrompt,
            string.Join(", ", availableTools));
        var userPrompt = $"任务描述: {taskDescription}\n请基于系统规则返回 JSON 决策。";

        var history = new ChatHistory(systemPrompt);
        history.AddUserMessage(userPrompt);

        var response = await _kernelService.GetChatCompletionAsync(
            history,
            providerId,
            cancellationToken: cancellationToken,
            enableAutoFunctionCalling: false,
            auditScope: new ModelInvocationScopeDto
            {
                OwnerType = ModelInvocationOwnerTypes.System,
                SceneCode = ModelInvocationSceneCodes.SystemOther,
                SceneCategory = ModelInvocationSceneCategories.Swarm,
                ResourceType = ModelInvocationResourceTypes.None
            });

        // 解析 JSON 响应
        try
        {
            var rawContent = response.Content?.Trim() ?? "{}";
            var content = rawContent.CleanJsonContent();
            
            var decision = JsonSerializer.Deserialize<ToolingDecision>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
            
            if (decision?.ToolCall != null && !string.IsNullOrEmpty(decision.ToolCall.ToolName))
            {
                var tool = decision.ToolCall.ToolName;
                return availableTools.Contains(tool) ? new List<string> { tool } : new List<string>();
            }

            return new List<string>();
        }
        catch (JsonException jsonEx)
        {
            var rawContent = response.Content ?? "{}"; // Changed default to empty JSON object
            _logger.LogWarning(jsonEx, "LLM 返回的 JSON 格式无效: {Content}", 
                rawContent.Length > 200 ? rawContent.Substring(0, 200) + "..." : rawContent);
            // 降级：从文本中尝试匹配工具名
            return availableTools.Where(t => rawContent.Contains(t)).Take(1).ToList();
        }
    }

    private class ToolingDecision
    {
        public string ThoughtProcess { get; set; } = string.Empty;
        public bool RequiresTool { get; set; }
        public ToolCallDetails? ToolCall { get; set; }
    }

    private class ToolCallDetails
    {
        public string ToolName { get; set; } = string.Empty;
        public Dictionary<string, object>? Parameters { get; set; }
    }
}
