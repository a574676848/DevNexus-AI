// using DevNexus.Domain.Abstractions via GlobalUsings
// using DevNexus.Domain.Configuration via GlobalUsings
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Constants;

namespace DevNexus.Infrastructure.Services.Embedding;

/// <summary>
/// Embedding 提供商基类
/// 提取 OpenAI 和 Doubao 等 Provider 的共同逻辑
/// </summary>
public abstract class EmbeddingProviderBase : IEmbeddingProvider
{
    protected readonly HttpClient HttpClient;
    protected readonly EmbeddingProviderConfig Config;
    protected readonly ILogger Logger;
    private readonly Guid _providerKey;

    public abstract string ProviderName { get; }
    public Guid ProviderKey => _providerKey;
    public string ModelName => Config.Model;
    public int VectorSize => Config.VectorSize;

    protected EmbeddingProviderBase(
        IHttpClientFactory httpClientFactory,
        EmbeddingProviderConfig config,
        ILogger logger,
        string httpClientName,
        Guid providerKey)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(logger);

        HttpClient = httpClientFactory.CreateClient(httpClientName);
        ConfigureHttpClient(config);

        Config = config;
        Logger = logger;
        _providerKey = providerKey;

        LogInitialization(httpClientName);
    }

    /// <summary>
    /// 配置 HttpClient（BaseUrl、超时、请求头）
    /// </summary>
    protected virtual void ConfigureHttpClient(EmbeddingProviderConfig config)
    {
        var baseUrl = config.BaseUrl.TrimEnd('/') + "/";
        HttpClient.BaseAddress = new Uri(baseUrl);

        if (config.TimeoutSeconds > 60)
        {
            HttpClient.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
        }

        HttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");
    }

    /// <summary>
    /// 记录初始化信息（供子类覆写自定义日志）
    /// </summary>
    protected virtual void LogInitialization(string httpClientName)
    {
        Logger.LogDebug(
            "[Embedding.{Provider}] Initialized | Model={Model} | VectorSize={VectorSize} | Timeout={Timeout}s | BaseUrl={BaseUrl}",
            ProviderName,
            Config.Model,
            Config.VectorSize,
            HttpClient.Timeout.TotalSeconds,
            HttpClient.BaseAddress
        );
    }

    /// <summary>
    /// 生成文本向量
    /// </summary>
    public abstract Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量生成文本向量
    /// </summary>
    public abstract Task<IList<ReadOnlyMemory<float>>> GenerateBatchEmbeddingsAsync(
        IList<string> texts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 发送 HTTP 请求
    /// </summary>
    protected async Task<HttpResponseMessage> SendRequestAsync(
        string endpoint,
        HttpContent content,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await HttpClient.PostAsync(endpoint, content, cancellationToken);
        }
        catch (HttpRequestException httpEx)
        {
            Logger.LogError(
                httpEx,
                "[Embedding.{Provider}.NetworkError] HTTP request failed | Url={Url} | Message={Message}",
                ProviderName,
                HttpClient.BaseAddress + endpoint,
                httpEx.Message
            );
            throw new InvalidOperationException(
                $"Network error while calling {ProviderName} embedding API: {httpEx.Message}. Check network connectivity and API endpoint.",
                httpEx
            );
        }

        Logger.LogDebug(
            "[Embedding.{Provider}] Response received | StatusCode={StatusCode} | ContentLength={ContentLength}",
            ProviderName,
            response.StatusCode,
            response.Content.Headers.ContentLength ?? -1
        );

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            Logger.LogError(
                "[Embedding.{Provider}.ApiError] API returned error | StatusCode={StatusCode} | Body={Body}",
                ProviderName,
                response.StatusCode,
                errorBody
            );
            response.EnsureSuccessStatusCode();
        }

        return response;
    }

    /// <summary>
    /// 解析 API 响应
    /// </summary>
    protected async Task<T> ParseResponseAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken) where T : EmbeddingResponseBase
    {
        T? result;
        try
        {
            result = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
        }
        catch (Exception parseEx)
        {
            var rawContent = await response.Content.ReadAsStringAsync(cancellationToken);
            Logger.LogError(
                parseEx,
                "[Embedding.{Provider}.ParseError] Failed to parse response | Content={Content}",
                ProviderName,
                rawContent.Length > 500 ? rawContent[..500] + "..." : rawContent
            );
            throw new InvalidOperationException(
                $"Failed to parse {ProviderName} embedding response: {parseEx.Message}",
                parseEx
            );
        }

        if (result == null)
        {
            throw new InvalidOperationException($"{ProviderName} embedding response is null");
        }

        // 检查 Data 属性
        var dataProperty = typeof(T).GetProperty("Data");
        if (dataProperty != null)
        {
            var data = dataProperty.GetValue(result) as System.Collections.IList;
            if (data == null || data.Count == 0)
            {
                throw new InvalidOperationException($"{ProviderName} embedding response data is empty");
            }
        }

        return result;
    }

    /// <summary>
    /// 检查向量维度是否匹配
    /// </summary>
    protected void LogVectorSizeMismatch(int actualSize)
    {
        if (actualSize != Config.VectorSize)
        {
            Logger.LogWarning(
                "[Embedding.{Provider}] Vector size mismatch | Expected={Expected} | Actual={Actual}",
                ProviderName,
                Config.VectorSize,
                actualSize
            );
        }
    }

    /// <summary>
    /// 记录成功生成向量
    /// </summary>
    protected void LogEmbeddingGenerated(int vectorSize, int? totalTokens = null)
    {
        Logger.LogDebug(
            "[Embedding.{Provider}] Embedding generated | VectorSize={Size} | Usage={Usage}",
            ProviderName,
            vectorSize,
            totalTokens ?? 0
        );
    }
}
