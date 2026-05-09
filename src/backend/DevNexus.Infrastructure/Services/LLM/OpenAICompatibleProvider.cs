// using DevNexus.Domain.Abstractions via GlobalUsings
// using DevNexus.Domain.Configuration via GlobalUsings
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using DevNexus.Shared.Constants;

#pragma warning disable SKEXP0010

namespace DevNexus.Infrastructure.Services.LLM;

/// <summary>
/// OpenAI Compatible LLM 提供商
/// 支持 OpenAI API 兼容的服务（如 MODELSCOPE、Ollama 等）
/// </summary>
public class OpenAICompatibleProvider : ILLMProvider
{
    private readonly LLMProviderConfig _config;
    private readonly ILogger<OpenAICompatibleProvider> _logger;
    private readonly IChatCompletionService _chatCompletionService;

    /// <summary>
    /// 提供商名称
    /// </summary>
    public string ProviderName => "OpenAICompatible";

    /// <summary>
    /// OpenAI 兼容提供商支持 SK 自动函数调用。
    /// </summary>
    public bool SupportsAutoFunctionCalling => true;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="config">提供商配置</param>
    /// <param name="logger">日志记录器</param>
    public OpenAICompatibleProvider(
        IHttpClientFactory httpClientFactory,
        LLMProviderConfig config,
        ILogger<OpenAICompatibleProvider> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // 规范化 BaseUrl：保留或补齐 /v1，只剥离更深层的 completions 路径
        // 这样可以同时兼容官方 OpenAI 端点和常见 OpenAI-Compatible 代理端点
        var baseUrl = NormalizeBaseUrl(_config.BaseUrl);
        _config.BaseUrl = baseUrl;

        // 使用 IHttpClientFactory 创建命名的 HttpClient
        var httpClient = httpClientFactory.CreateClient(HttpClientNames.LLMProvider);
        
        // 动态配置 BaseAddress
        try 
        {
            httpClient.BaseAddress = new Uri(_config.BaseUrl);
        }
        catch (UriFormatException ex)
        {
           _logger.LogError(ex, "Invalid BaseUrl format: {BaseUrl}", _config.BaseUrl);
           throw;
        }

        // 创建 OpenAI Chat Completion Service
        // 使用 HttpClient 注入以强制使用 Standard OpenAI 模式 (Bearer Token)
        _chatCompletionService = new OpenAIChatCompletionService(
            modelId: _config.Model,
            apiKey: _config.ApiKey,
            httpClient: httpClient,
            loggerFactory: null 
        );

        _logger.LogDebug(
            "[AI.LLM] OpenAI Compatible Provider initialized | BaseUrl={BaseUrl} Model={Model}",
            _config.BaseUrl,
            _config.Model);
    }

    /// <summary>
    /// 规范化 OpenAI 兼容端点。
    /// </summary>
    private static string NormalizeBaseUrl(string baseUrl)
    {
        var url = baseUrl.Trim();
        var suffixesToRemove = new[] { "/v1/chat/completions", "/chat/completions", "/completions" };

        foreach (var suffix in suffixesToRemove)
        {
            if (url.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                url = url[..^suffix.Length];
                break;
            }
        }

        // 检查 URL 是否已包含任意版本路径段（如 /v1、/v4、/v2 等）
        // 智谱等厂商使用 /v4，而非 OpenAI 的 /v1，不应强制追加 /v1
        if (!System.Text.RegularExpressions.Regex.IsMatch(url, @"/v\d+(?:/|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            url = url.TrimEnd('/') + "/v1";
        }

        return url.TrimEnd('/') + "/";
    }

    /// <summary>
    /// 获取聊天完成服务实例
    /// </summary>
    /// <returns>聊天完成服务</returns>
    public IChatCompletionService GetChatCompletionService()
    {
        return _chatCompletionService;
    }
}
