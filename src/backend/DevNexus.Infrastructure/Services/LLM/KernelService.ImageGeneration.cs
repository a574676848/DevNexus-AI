// using DevNexus.Domain.Abstractions via GlobalUsings
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.TextToImage;
using System.Text.Json;

namespace DevNexus.Infrastructure.Services.LLM;

/// <summary>
/// KernelService 图片生成相关方法
/// 采用 SK 最佳实践：在 Kernel 构建时注册服务，通过 GetRequiredService 获取
/// </summary>
public partial class KernelService
{
    // 缓存带有 TextToImage 服务的 Kernel 实例 (按 ProviderId)
    private readonly Dictionary<Guid, Kernel> _imageKernelCache = new();

    /// <summary>
    /// 生成图片（自动选择支持 Text-to-Image 的 Provider，包含降级策略）
    /// </summary>
    public async Task<ImageGenerationResult> GenerateImageAsync(
        string prompt,
        int width = 1024,
        int height = 1024,
        CancellationToken cancellationToken = default,
        ModelInvocationScopeDto? auditScope = null)
    {
        _logger.LogInformation("[AI.ImageGeneration] GenerateImageAsync called | Prompt={Prompt}", prompt);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // 1. 获取 Provider 管理服务
            var providerService = _serviceProvider.GetRequiredService<ILLMProviderManagementService>();
            var providerInfo = _providerFactory.GetCurrentProviderInfo();

            // 2. 获取当前 Provider 信息，检查是否支持 TextToImage
            var currentProviderInfo = _providerFactory.GetCurrentProviderInfo();

            var allProviders = await providerService.GetAllProvidersAsync(cancellationToken: cancellationToken);

            LLMProviderResponse? targetProvider = allProviders
                .Where(p => p.IsEnabled && HasCapability(p, "TextToImage"))
                .OrderBy(p => p.Priority)
                .FirstOrDefault();

            // 4. 如果仍未找到支持的 Provider
            if (targetProvider == null)
            {
                _logger.LogWarning("[AI.ImageGeneration] No provider with 'TextToImage' capability found.");
                return new ImageGenerationResult
                {
                    Success = false,
                    Error = "抱歉，当前模型暂不支持画图功能。您可以切换到支持该能力的供应商（如 OpenAI DALL-E），或尝试访问 Midjourney、Stability AI 等专业平台。"
                };
            }

            // 5. 获取或创建带有 TextToImage 服务的 Kernel (SK 最佳实践)
            var imageKernel = await GetOrCreateImageKernelAsync(targetProvider.Id, cancellationToken);

            // 6. 通过 SK 标准方式获取 ITextToImageService
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only
            var textToImageService = imageKernel.GetRequiredService<ITextToImageService>();

            // 7. 调用 SK 生成图片
            _logger.LogInformation(
                "[AI.ImageGeneration] Generating image via SK | ProviderId={ProviderId} ModelName={ModelName} Width={Width} Height={Height}",
                targetProvider.Id, targetProvider.ModelName, width, height);

            var imageUrl = await textToImageService.GenerateImageAsync(prompt, width, height, cancellationToken: cancellationToken);
#pragma warning restore SKEXP0001

            if (string.IsNullOrEmpty(imageUrl))
            {
                throw new InvalidOperationException("文生图服务返回空 URL");
            }

            _logger.LogInformation("[AI.ImageGeneration] Image generated successfully | Url={Url}", imageUrl);

            stopwatch.Stop();
            _tokenAuditService.RecordStreamingCompletion(
                sessionId: auditScope?.SessionId,
                messageId: auditScope?.MessageId,
                userId: auditScope?.OwnerUserId,
                modelId: targetProvider.ModelName ?? providerInfo?.ModelName ?? "unknown",
                providerName: targetProvider.DisplayName,
                providerType: ModelInvocationProviderTypes.Llm,
                providerId: targetProvider.Id.ToString(),
                inputTokens: 0,
                outputTokens: 0,
                durationMs: stopwatch.ElapsedMilliseconds,
                invocationKind: ModelInvocationKinds.ImageGeneration,
                sceneCode: auditScope?.SceneCode ?? ModelInvocationSceneCodes.GenerationImageCreate,
                sceneCategory: auditScope?.SceneCategory ?? ModelInvocationSceneCategories.UserFacing,
                resourceType: auditScope?.ResourceType ?? ModelInvocationResourceTypes.Session,
                resourceId: auditScope?.ResourceId ?? auditScope?.SessionId?.ToString(),
                usageSource: ModelInvocationUsageSources.None,
                status: ModelInvocationStatuses.Succeeded,
                meteringType: ModelInvocationMeteringTypes.Image,
                meteringValue: 1m);

            return new ImageGenerationResult
            {
                Success = true,
                ImageUrl = imageUrl,
                Markdown = $"![Generated Image]({imageUrl})",
                ProviderName = targetProvider.DisplayName
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _tokenAuditService.RecordStreamingCompletion(
                sessionId: auditScope?.SessionId,
                messageId: auditScope?.MessageId,
                userId: auditScope?.OwnerUserId,
                modelId: _providerFactory.GetCurrentProviderInfo()?.ModelName ?? "unknown",
                providerName: _providerFactory.GetCurrentProviderInfo()?.ProviderName ?? "unknown",
                providerType: ModelInvocationProviderTypes.Llm,
                providerId: _providerFactory.GetCurrentProviderInfo()?.LLMProviderId.ToString() ?? Guid.Empty.ToString(),
                inputTokens: 0,
                outputTokens: 0,
                durationMs: stopwatch.ElapsedMilliseconds,
                invocationKind: ModelInvocationKinds.ImageGeneration,
                sceneCode: auditScope?.SceneCode ?? ModelInvocationSceneCodes.GenerationImageCreate,
                sceneCategory: auditScope?.SceneCategory ?? ModelInvocationSceneCategories.UserFacing,
                resourceType: auditScope?.ResourceType ?? ModelInvocationResourceTypes.Session,
                resourceId: auditScope?.ResourceId ?? auditScope?.SessionId?.ToString(),
                usageSource: ModelInvocationUsageSources.None,
                status: ModelInvocationStatuses.Failed,
                errorCode: ex.GetType().Name,
                errorMessage: ex.Message,
                meteringType: ModelInvocationMeteringTypes.Image,
                meteringValue: 1m);
            _logger.LogError(ex, "[AI.ImageGeneration] Failed to generate image | Prompt={Prompt}", prompt);
            return new ImageGenerationResult
            {
                Success = false,
                Error = $"生成图片时发生错误：{ex.Message}"
            };
        }
    }

    /// <summary>
    /// 获取或创建带有 ITextToImageService 的 Kernel 实例 (SK 最佳实践)
    /// </summary>
    private async Task<Kernel> GetOrCreateImageKernelAsync(Guid providerId, CancellationToken cancellationToken)
    {
        // 检查缓存
        if (_imageKernelCache.TryGetValue(providerId, out var cachedKernel))
        {
            return cachedKernel;
        }

        // 获取 Provider 信息
        var providerService = _serviceProvider.GetRequiredService<ILLMProviderManagementService>();
        var providerDto = await providerService.GetProviderByIdAsync(providerId, cancellationToken);

        if (providerDto == null)
        {
            throw new KeyNotFoundException($"LLM Provider not found: {providerId}");
        }

        // 获取解密的 API Key
        var apiKey = await providerService.GetDecryptedApiKeyAsync(providerId, cancellationToken);

        // 确定基础 URL 和模型
        var baseUrl = NormalizeEndpoint(providerDto.Endpoint);
        var modelId = string.IsNullOrEmpty(providerDto.ModelName) ? "dall-e-3" : providerDto.ModelName;

        _logger.LogInformation(
            "[AI.ImageGeneration] Creating Image Kernel | Endpoint={Endpoint} Model={Model}",
            baseUrl, modelId);

        // 创建配置了自定义 BaseAddress 的 HttpClient
        var httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(baseUrl);

        // 使用 SK 最佳实践：通过 Kernel Builder 注册服务
        var builder = Kernel.CreateBuilder();

#pragma warning disable SKEXP0001, SKEXP0010 // Type is for evaluation purposes only

        bool isDashScope = baseUrl.Contains("dashscope.aliyuncs.com", StringComparison.OrdinalIgnoreCase);

        if (isDashScope)
        {
            _logger.LogInformation("[AI.ImageGeneration] Using BailianTextToImageService for Aliyun API.");
            var textToImageService = new BailianTextToImageService(
                apiKey: apiKey,
                modelId: modelId,
                httpClient: httpClient,
                loggerFactory: _serviceProvider.GetRequiredService<ILoggerFactory>());

            builder.Services.AddSingleton<ITextToImageService>(textToImageService);
        }
        else
        {
            // 创建 OpenAITextToImageService 并注册到 Kernel 的服务容器
            var textToImageService = new OpenAITextToImageService(
                apiKey: apiKey,
                modelId: modelId,
                httpClient: httpClient);

            builder.Services.AddSingleton<ITextToImageService>(textToImageService);
        }
#pragma warning restore SKEXP0001, SKEXP0010

        var kernel = builder.Build();

        // 缓存 Kernel
        _imageKernelCache[providerId] = kernel;

        return kernel;
    }

    /// <summary>
    /// 规范化 Endpoint URL
    /// </summary>
    private static string NormalizeEndpoint(string? endpoint)
    {
        if (string.IsNullOrEmpty(endpoint))
        {
            return "https://api.openai.com/v1";
        }

        var url = endpoint.TrimEnd('/');

        // 移除常见的 API 路径后缀
        foreach (var suffix in new[] { "/chat/completions", "/images/generations", "/completions" })
        {
            if (url.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                url = url.Substring(0, url.Length - suffix.Length);
            }
        }

        return url.TrimEnd('/') + "/";
    }

    /// <summary>
    /// 检查 Provider 是否具备指定能力
    /// </summary>
    private static bool HasCapability(LLMProviderResponse provider, string capability)
    {
        if (provider.Configuration == null) return false;

        if (provider.Configuration.TryGetValue("Capabilities", out var capsObj))
        {
            if (capsObj is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in jsonElement.EnumerateArray())
                {
                    if (item.GetString()?.Equals(capability, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        return true;
                    }
                }
            }
            else if (capsObj is IEnumerable<string> list)
            {
                return list.Contains(capability, StringComparer.OrdinalIgnoreCase);
            }
        }
        return false;
    }

    /// <summary>
    /// 清除 Image Kernel 缓存
    /// </summary>
    public void InvalidateImageKernelCache(Guid? providerId = null)
    {
        if (providerId.HasValue)
        {
            _imageKernelCache.Remove(providerId.Value);
            _logger.LogInformation("[AI.ImageGeneration] Invalidated Image Kernel cache for provider: {ProviderId}", providerId);
        }
        else
        {
            _imageKernelCache.Clear();
            _logger.LogInformation("[AI.ImageGeneration] Invalidated all Image Kernel cache");
        }
    }
}
