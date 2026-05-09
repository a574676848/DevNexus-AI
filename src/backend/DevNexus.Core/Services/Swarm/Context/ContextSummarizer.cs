using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;
using DevNexus.Domain.Abstractions;
using Microsoft.Extensions.Logging;
using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Services.Swarm.Context;

/// <summary>
/// 上下文压缩与摘要服务接口
/// </summary>
public interface IContextSummarizer
{
    /// <summary>
    /// 压缩或摘要任务输出内容，以适应 Token 限制
    /// </summary>
    Task<string> SummarizeOutputAsync(string taskId, string content, CancellationToken cancellationToken = default);
}

/// <summary>
/// 基于规则的简单压缩器 (兜底方案)
/// </summary>
public class RuleBasedContextSummarizer : IContextSummarizer
{
    public Task<string> SummarizeOutputAsync(string taskId, string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(content)) return Task.FromResult(string.Empty);
        if (content.Length < 1000) return Task.FromResult(content);

        var head = content.Substring(0, 500);
        var tail = content.Substring(content.Length - 500);

        var summary = new StringBuilder();
        summary.AppendLine(head);
        summary.AppendLine();
        summary.AppendLine("... [Content Truncated due to extreme length] ...");
        summary.AppendLine();
        summary.AppendLine(tail);

        return Task.FromResult(summary.ToString());
    }
}

/// <summary>
/// 基于 LLM 的语义摘要服务 (#8)
/// </summary>
public class LlmContextSummarizer : IContextSummarizer
{
    private readonly IKernelService _kernelService;
    private readonly ILogger<LlmContextSummarizer> _logger;

    public LlmContextSummarizer(IKernelService kernelService, ILogger<LlmContextSummarizer> logger)
    {
        _kernelService = kernelService;
        _logger = logger;
    }

    public async Task<string> SummarizeOutputAsync(string taskId, string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;
        
        // 阈值：超过 2000 字符才进行摘要，否则保留原文
        if (content.Length < 2000) return content;

        _logger.LogInformation("Generating semantic summary for task {TaskId} context (Length: {Length}).", taskId, content.Length);

        var prompt = $"""
            Summarize the following technical task output or discussion logs. 
            Maintain key decisions, critical data, code identifiers, and the final state/result.
            The summary should be concise but semantically rich.

            CONTENT TO SUMMARIZE:
            ---
            {content}
            ---

            Provide only the summary text without any extra chat bubbles or headers.
            """;

        try
        {
            var summary = await _kernelService.GenerateTextAsync(
                prompt,
                cancellationToken: cancellationToken,
                auditScope: new ModelInvocationScopeDto
                {
                    OwnerType = ModelInvocationOwnerTypes.System,
                    SceneCode = ModelInvocationSceneCodes.ContextSummary,
                    SceneCategory = ModelInvocationSceneCategories.Swarm,
                    ResourceType = ModelInvocationResourceTypes.ContextWorkPackageRecord,
                    ResourceId = taskId
                });
            return summary.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM summarization failed for task {TaskId}. Falling back to truncation.", taskId);
            // 语义摘要失败时 fallback 到物理截断
            var head = content[..Math.Min(content.Length, 800)];
            var tail = content.Length > 800 ? content[^Math.Min(content.Length - 800, 800)..] : "";
            return $"{head}\n\n... [Semantic Summarization Failed, Truncated] ...\n\n{tail}";
        }
    }
}
