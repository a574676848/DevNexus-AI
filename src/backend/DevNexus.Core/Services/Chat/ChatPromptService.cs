// using DevNexus.Domain.Abstractions via GlobalUsings
using DevNexus.Shared.Constants;
using System.Globalization;
using System.Runtime.InteropServices;
using DevNexus.Shared.DTOs; // For LLMProviderResponse
using Microsoft.Extensions.Logging;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 聊天提示词服务 - 负责构建系统提示词和上下文管理
/// 采用 RAG 优先策略，不再全量加载文档
/// </summary>
public class ChatPromptService
{
    private readonly ILLMProviderManagementService _llmProviderService;
    private readonly ArtifactContextStrategy _artifactContextStrategy;
    private readonly ILogger<ChatPromptService> _logger;

    public ChatPromptService(
        ILLMProviderManagementService llmProviderService,
        ArtifactContextStrategy artifactContextStrategy,
        ILogger<ChatPromptService> logger)
    {
        _llmProviderService = llmProviderService;
        _artifactContextStrategy = artifactContextStrategy;
        _logger = logger;
    }

    /// <summary>
    /// 获取系统身份提示词（仅包含系统指令）
    /// </summary>
    public string GetSystemIdentity()
    {
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss dddd", CultureInfo.CurrentCulture);
        var os = RuntimeInformation.OSDescription;
        return string.Format(PromptConstants.System.SystemBasePrompt, now, os);
    }

    /// <summary>
    /// 获取 Artifacts 上下文消息（User Role）- 使用 RAG 优先策略
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="userId">用户ID（用于 RAG 检索隔离）</param>
    /// <param name="providerId">Provider ID</param>
    /// <param name="currentMessage">当前用户消息（用于 RAG 检索）</param>
    /// <param name="enableRag">是否启用知识库检索</param>
    /// <param name="activeArtifactIds">当前请求显式激活的 Artifact 列表（优先全文注入）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<string?> GetArtifactsContextMessageAsync(
        Guid sessionId,
        Guid userId,
        Guid? providerId,
        string? currentMessage,
        bool enableRag,
        IEnumerable<Guid>? activeArtifactIds,
        CancellationToken cancellationToken)
    {
        var normalizedActiveIds = activeArtifactIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        // 显式关闭 RAG 且没有活跃附件时，直接跳过文档上下文注入
        if (!enableRag && (normalizedActiveIds == null || normalizedActiveIds.Count == 0))
        {
            _logger.LogDebug(
                "[AI.Chat] RAG disabled and no active artifacts | SessionId={SessionId}",
                sessionId);
            return null;
        }

        // 获取 Provider 信息并估算 Token 上限
        int maxContextTokens = 128000; // 默认 128k
        if (providerId.HasValue)
        {
            try
            {
                var provider = await _llmProviderService.GetProviderByIdAsync(providerId.Value, cancellationToken);
                maxContextTokens = EstimateContextWindow(provider);
                _logger.LogDebug("[AI.Chat] AI 聊天： Estimated context window: {Tokens} tokens for model {Model}", 
                    maxContextTokens, provider?.ModelName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AI.Chat] Failed to get provider info for token estimation, using default 128k");
            }
        }

        // 策略配置：预留 30% 安全空间给历史对话和输出
        int safeBuffer = (int)(maxContextTokens * 0.3);
        int availableTokensForArtifacts = Math.Max(4000, maxContextTokens - safeBuffer);

        // 使用 RAG 优先策略（当 enableRag=false 时只保留活跃附件全文，不做知识库检索）
        try
        {
            var ragConfig = new ArtifactContextStrategy.RagConfig
            {
                EnableKnowledgeRetrieval = enableRag,
                IncludeDocumentIndex = enableRag
            };

            var ragContext = await _artifactContextStrategy.BuildRagContextAsync(
                sessionId,
                userId,
                currentMessage: enableRag ? currentMessage : null,
                activeArtifactIds: normalizedActiveIds,
                availableTokensForArtifacts,
                config: ragConfig,
                cancellationToken);
            
            if (!string.IsNullOrEmpty(ragContext))
            {
                _logger.LogDebug("[AI.Chat] AI 聊天： Built RAG context | SessionId={SessionId} Tokens~={Tokens}", 
                    sessionId, EstimateTokenCount(ragContext));
                return ragContext;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AI.Chat] RAG context strategy failed | SessionId={SessionId}", sessionId);
        }

        return null;
    }

    /// <summary>
    /// 估算模型的上下文窗口大小
    /// </summary>
    public int EstimateContextWindow(LLMProviderResponse? provider)
    {
        if (provider == null) return 128000;

        // 1. 尝试从配置中读取
        if (provider.Configuration != null)
        {
            var keysToCheck = new[] { "maxTokens", "MaxTokens", "context_window", "ContextWindow" };
            
            foreach (var key in keysToCheck)
            {
                if (provider.Configuration.TryGetValue(key, out var valObj) &&
                    int.TryParse(valObj.ToString(), out var val) && val > 0)
                {
                    return val;
                }
            }
        }

        // 2. 基于模型名称的启发式估算 (Fallback)
        var model = provider.ModelName ?? "";
        
        if (model.Contains("gemini", StringComparison.OrdinalIgnoreCase)) return 1000000; // 1M+
        if (model.Contains("gpt", StringComparison.OrdinalIgnoreCase) 
            || model.Contains("128k", StringComparison.OrdinalIgnoreCase)) return 128000;
        if (model.Contains("deepseek", StringComparison.OrdinalIgnoreCase) 
            || model.Contains("qwen", StringComparison.OrdinalIgnoreCase)) return 64000;
        if (model.Contains("32k", StringComparison.OrdinalIgnoreCase)) return 32000;
        if (model.Contains("16k", StringComparison.OrdinalIgnoreCase)) return 16000;
        
        return 32000; // 默认回退值
    }

    /// <summary>
    /// 估算文本的 Token 数量
    /// </summary>
    private static int EstimateTokenCount(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        
        var chineseCount = text.Count(c => c >= 0x4E00 && c <= 0x9FFF);
        var otherCount = text.Length - chineseCount;
        
        return (int)Math.Ceiling(chineseCount / 1.5) + (int)Math.Ceiling(otherCount / 4.0);
    }
}
