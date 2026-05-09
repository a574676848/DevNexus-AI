using DevNexus.Infrastructure.Services.LLM;
using DevNexus.Infrastructure.Services.Parsing.PaddleOCR;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace DevNexus.Infrastructure.Services.Parsing;

/// <summary>
/// Vision 解析服务
/// 实现图片/视觉文档解析的 Fallback 链：
/// 1. PaddleOCR 提取文本 (快速、低成本)
/// 2. 如果需要描述或 OCR 失败 -> Vision LLM (高成本)
/// </summary>
public class VisionParsingService
{
    private readonly ILLMProviderManagementService _llmProviderService;
    private readonly KernelService _kernelService;
    private readonly IPaddleOcrClient _paddleOcrClient;
    private readonly ILogger<VisionParsingService> _logger;
    private readonly IDistributedCache _cache;

    private const string ImageDescriptionPrompt = PromptConstants.Vision.StructuredExtractionPrompt;

    public VisionParsingService(
        ILLMProviderManagementService llmProviderService,
        KernelService kernelService,
        IPaddleOcrClient paddleOcrClient,
        ILogger<VisionParsingService> logger,
        IDistributedCache cache)
    {
        _llmProviderService = llmProviderService;
        _kernelService = kernelService;
        _paddleOcrClient = paddleOcrClient;
        _logger = logger;
        _cache = cache;
    }

    /// <summary>
    /// 解析图片并生成描述/文本
    /// </summary>
    public async Task<VisionParsingResult> ParseImageAsync(
        byte[] imageBytes,
        string mimeType,
        Guid? sessionProviderId = null,
        ParsingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var contentHash = ComputeHash(imageBytes);
        var cacheKey = $"vision:result:{contentHash}";
        var warnings = new List<string>();

        // 1. 检查缓存
        var cachedResult = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedResult))
        {
            return new VisionParsingResult
            {
                Success = true,
                Description = cachedResult,
                ProcessingTimeMs = 0,
                ModelUsed = "cache",
                Strategy = "cached-result",
                Warnings = warnings
            };
        }

        VisionParsingResult? finalResult = null;

        // === 策略 1: 尝试 PaddleOCR 获取文本 (如果只是想要文字) ===
        // 注意：如果目标是"图片描述"(scene description)，OCR 可能不够，
        // 但如果目标是"图片转文字"(document parsing)，OCR 是首选。
        // 为了兼容旧逻辑（返回 Description），我们这里先尝试 Vision LLM 获取描述，
        // 如果 Vision LLM 失败或不需要描述，回退到 OCR。

        // 实际上，对于 ParseImageAsync，通常期望得到对图片的描述或完整文本。
        // 我们可以双管齐下：先 OCR，如有大量文字直接返回；否则 Vision LLM。
        // 或者保留原有逻辑：优先 LLM (更智能)，兜底 OCR。

        // 按照用户需求：结果合并：PaddleOCR 返回的文本，调用大模型整理。

        // 这里我们优先尝试 LLM (为了获得 Image Description)，
        // 如果 LLM 失败，使用 PaddleOCR 作为兜底。

        // 1.1 尝试会话 Provider (Vision LLM)
        if (sessionProviderId.HasValue)
        {
            finalResult = await TryCallVisionLlmAsync(sessionProviderId.Value, imageBytes, mimeType, ImageDescriptionPrompt, "session-provider", warnings, cancellationToken);
        }

        // 1.2 尝试备用 Vision Provider
        if (finalResult == null)
        {
            var visionProvider = await FindVisionCapableProviderAsync(cancellationToken);
            if (visionProvider != null)
            {
                finalResult = await TryCallVisionLlmAsync(visionProvider.Id, imageBytes, mimeType, ImageDescriptionPrompt, "fallback-vision-provider", warnings, cancellationToken);
            }
        }

        // 1.3 PaddleOCR 兜底
        if (finalResult == null)
        {
            try
            {
                _logger.LogDebug("Vision LLM 不可用或失败，使用 PaddleOCR 兜底");
                var ocrText = await _paddleOcrClient.RecognizeTextAsync(imageBytes, cancellationToken);

                if (!string.IsNullOrWhiteSpace(ocrText))
                {
                    finalResult = new VisionParsingResult
                    {
                        Success = true,
                        Description = $"[OCR Result]:\n{ocrText}",
                        ProcessingTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds,
                        ModelUsed = "paddle-ocr-3.2",
                        Strategy = "ocr-fallback",
                        Warnings = warnings
                    };
                }
                else
                {
                    finalResult = new VisionParsingResult
                    {
                        Success = false,
                        ErrorMessage = "OCR 未识别到文字",
                        Warnings = warnings
                    };
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"PaddleOCR 失败: {ex.Message}");
                finalResult = new VisionParsingResult
                {
                    Success = false,
                    ErrorMessage = "所有解析方法都失败了",
                    Warnings = warnings
                };
            }
        }

        // 写入缓存
        if (finalResult != null && finalResult.Success && !string.IsNullOrWhiteSpace(finalResult.Description))
        {
            await _cache.SetStringAsync(cacheKey, finalResult.Description, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
            }, cancellationToken);
        }

        return finalResult ?? new VisionParsingResult { Success = false, ErrorMessage = "Unknown error" };
    }

    /// <summary>
    /// 从图片中提取文本 (用于 PDF Fallback)
    /// **优先使用 PaddleOCR**
    /// </summary>
    public async Task<string> ExtractTextFromImageAsync(
        byte[] imageBytes,
        string mimeType,
        Guid? providerId = null,
        ParsingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // 1. 优先使用 PaddleOCR (速度快，成本低，适合纯文本提取)
        try
        {
            var ocrText = await _paddleOcrClient.RecognizeTextAsync(imageBytes, cancellationToken);
            if (!string.IsNullOrWhiteSpace(ocrText))
            {
                return ocrText;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PaddleOCR 提取文本失败，尝试 Vision LLM Fallback");
        }

        // 2. 如果 OCR 失败，尝试 Vision LLM
        var prompt = PromptConstants.Vision.PureTextExtractionPrompt;
        var warnings = new List<string>(); // 丢弃警告

        // 2.1 指定 Provider
        if (providerId.HasValue)
        {
            var result = await TryCallVisionLlmAsync(providerId.Value, imageBytes, mimeType, prompt, "extract-text-session", warnings, cancellationToken);
            if (result != null && result.Success) return result.Description ?? string.Empty;
        }

        // 2.2 备用 Provider
        var visionProvider = await FindVisionCapableProviderAsync(cancellationToken);
        if (visionProvider != null)
        {
            var result = await TryCallVisionLlmAsync(visionProvider.Id, imageBytes, mimeType, prompt, "extract-text-fallback", warnings, cancellationToken);
            if (result != null && result.Success) return result.Description ?? string.Empty;
        }

        return string.Empty;
    }

    /// <summary>
    /// 增强 ImageDocumentContent
    /// </summary>
    public async Task<ImageDocumentContent> EnhanceWithVisionAsync(
        ImageDocumentContent content,
        byte[] imageBytes,
        string mimeType,
        Guid? sessionProviderId = null,
        ParsingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ParseImageAsync(imageBytes, mimeType, sessionProviderId, options, cancellationToken);
        if (result.Success)
        {
            content.Description = result.Description;
        }
        return content;
    }

    #region Private Helpers

    private async Task<VisionParsingResult?> TryCallVisionLlmAsync(
        Guid providerId,
        byte[] imageBytes,
        string mimeType,
        string prompt,
        string strategy,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        try
        {
            var provider = await _llmProviderService.GetProviderByIdAsync(providerId, cancellationToken);
            if (provider == null || !HasVisionCapability(provider)) return null;

            var base64 = Convert.ToBase64String(imageBytes);
            var dataUrl = $"data:{mimeType};base64,{base64}";
            var startTime = DateTime.UtcNow;

            var result = await _kernelService.GetVisionChatCompletionAsync(prompt, dataUrl, providerId, cancellationToken);

            if (result?.Content != null)
            {
                return new VisionParsingResult
                {
                    Success = true,
                    Description = result.Content,
                    ProcessingTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds,
                    ModelUsed = provider.ModelName,
                    Strategy = strategy,
                    Warnings = warnings
                };
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"Vision ({strategy}) failed: {ex.Message}");
        }
        return null;
    }

    private async Task<Shared.DTOs.LLMProviderResponse?> FindVisionCapableProviderAsync(CancellationToken cancellationToken)
    {
        var providers = await _llmProviderService.GetAllProvidersAsync(cancellationToken: cancellationToken);
        return providers.OrderBy(o => o.Priority).FirstOrDefault(p => p.IsEnabled && HasVisionCapability(p));
    }

    private static bool HasVisionCapability(Shared.DTOs.LLMProviderResponse provider)
    {
        if (provider.Configuration == null || !provider.Configuration.TryGetValue("SupportsVision", out var value))
            return false;

        return value switch
        {
            bool boolValue => boolValue,
            string strValue => strValue.Equals("true", StringComparison.OrdinalIgnoreCase),
            System.Text.Json.JsonElement jsonElement => jsonElement.ValueKind == System.Text.Json.JsonValueKind.True
                || (jsonElement.ValueKind == System.Text.Json.JsonValueKind.String && jsonElement.GetString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true),
            _ => false
        };
    }

    private string ComputeHash(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    #endregion
}

public class VisionParsingResult
{
    public bool Success { get; set; }
    public string? Description { get; set; }
    public string? ErrorMessage { get; set; }
    public double ProcessingTimeMs { get; set; }
    public string? ModelUsed { get; set; }
    public string? Strategy { get; set; }
    public List<string> Warnings { get; set; } = new();
}
