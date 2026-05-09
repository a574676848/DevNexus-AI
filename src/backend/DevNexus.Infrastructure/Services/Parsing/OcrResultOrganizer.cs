using DevNexus.Domain.Abstractions;
using DevNexus.Infrastructure.Services.LLM;
#pragma warning disable SKEXP0050
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Text;
using System.Text;

namespace DevNexus.Infrastructure.Services.Parsing;

/// <summary>
/// OCR 结果整理器接口
/// </summary>
public interface IOcrResultOrganizer
{
    Task<string> OrganizeAsync(string ocrText, Guid? providerId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// 使用 LLM 整理 OCR 结果
/// </summary>
public class OcrResultOrganizer : IOcrResultOrganizer
{
    private readonly ILLMProviderManagementService _llmProviderService;
    private readonly IKernelService _kernelService;
    private readonly ILogger<OcrResultOrganizer> _logger;

    public OcrResultOrganizer(
        ILLMProviderManagementService llmProviderService,
        KernelService kernelService,
        ILogger<OcrResultOrganizer> logger)
    {
        _llmProviderService = llmProviderService;
        _kernelService = kernelService;
        _logger = logger;
    }

    public async Task<string> OrganizeAsync(string ocrText, Guid? providerId = null, CancellationToken cancellationToken = default)
    {
        // 移除硬编码截断逻辑，改用分块处理
        // 设置每块的最大 Token 数（预留给 Prompt 和 Output）
        // 假设模型上下文窗口至少 8k-16k，安全起见每块 4000 token（约 6000-8000 中文字符）
        const int MaxTokensPerChunk = 4000;
        
        try
        {
            LLMProviderResponse? provider = null;

            // 1. 优先使用指定的 Provider
            if (providerId.HasValue)
            {
                provider = await _llmProviderService.GetProviderByIdAsync(providerId.Value, cancellationToken);
            }

            // 2. 如果未指定或未找到，使用第一个启用的 Provider
            if (provider == null || !provider.IsEnabled)
            {
                var providers = await _llmProviderService.GetAllProvidersAsync(false, cancellationToken);
                provider = providers.FirstOrDefault(p => p.IsEnabled);
            }

            if (provider == null)
            {
                _logger.LogWarning("未找到可用 LLM Provider，跳过 OCR 整理");
                return ocrText;
            }

            // 使用 Semantic Kernel 的 TextChunker 进行分块
            // 先按行切分，每行最大 1000 token
            var lines = TextChunker.SplitPlainTextLines(ocrText, maxTokensPerLine: 1000);
            // 再按段落切分，每段最大 4000 token
            var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, maxTokensPerParagraph: MaxTokensPerChunk);

            if (paragraphs.Count == 0) return ocrText;

            // 如果只有一段，直接处理
            if (paragraphs.Count == 1)
            {
                return await ProcessSegmentAsync(paragraphs[0], provider.Id, cancellationToken);
            }

            _logger.LogDebug("OCR 文本较长，切分为 {Count} 个片段进行处理", paragraphs.Count);
            
            var sb = new StringBuilder();
            for (int i = 0; i < paragraphs.Count; i++)
            {
                _logger.LogDebug("正在处理第 {Index}/{Total} 个片段...", i + 1, paragraphs.Count);
                var segmentResult = await ProcessSegmentAsync(paragraphs[i], provider.Id, cancellationToken);
                sb.AppendLine(segmentResult);
                
                // 简单的防速率限制延迟
                if (i < paragraphs.Count - 1) await Task.Delay(500, cancellationToken); 
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCR 结果整理失败");
            return ocrText; // 失败时返回原始内容
        }
    }

    private async Task<string> ProcessSegmentAsync(string text, Guid providerId, CancellationToken cancellationToken)
    {
        try 
        {
            var prompt = string.Format(PromptConstants.Vision.OcrMarkdownCleanupPrompt, text);
            var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
            chatHistory.AddUserMessage(prompt);

            var result = await _kernelService.GetChatCompletionAsync(
                chatHistory,
                providerId,
                auditScope: new ModelInvocationScopeDto
                {
                    OwnerType = ModelInvocationOwnerTypes.System,
                    SceneCode = ModelInvocationSceneCodes.ParsingOcrCleanup,
                    SceneCategory = ModelInvocationSceneCategories.Parsing,
                    ResourceType = ModelInvocationResourceTypes.Artifact
                },
                cancellationToken: cancellationToken);
            return result?.Content ?? text;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "片段整理失败，保留原文");
            return text;
        }
    }
}
