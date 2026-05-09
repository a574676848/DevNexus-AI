using System.Net.Http.Headers;
using DevNexus.Core.Abstractions.Search;
using DevNexus.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Search;

/// <summary>
/// Jina AI 阅读器实现 - 快速网页转 Markdown
/// 调用 https://r.jina.ai/{url} 获取纯文本内容，100万次/月免费额度
/// </summary>
public class JinaReaderEngine : IWebReaderEngine
{
    /// <summary>
    /// Jina Reader 官方默认端点
    /// 用户可以在供应商配置中覆盖此地址（支持本地部署或代理）
    /// </summary>
    private const string DefaultJinaReaderUrl = "https://r.jina.ai/";
    private const int MaxContentLength = 15000; // 防止超长文本，限制 15000 字符

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<JinaReaderEngine> _logger;

    public JinaReaderEngine(IHttpClientFactory httpClientFactory, ILogger<JinaReaderEngine> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> ReadWebpageAsync(string url, SearchProvider config)
    {
        if (string.IsNullOrEmpty(url))
        {
            throw new ArgumentException("URL cannot be empty.");
        }

        try
        {
            // 验证 URL 格式
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                throw new ArgumentException($"Invalid URL format: {url}");
            }

            var client = _httpClientFactory.CreateClient("Jina");
            
            // 优先从配置中获取 Endpoint，否则使用默认端点
            var baseUrl = string.IsNullOrEmpty(config.Endpoint) 
                ? DefaultJinaReaderUrl 
                : config.Endpoint.TrimEnd('/');
            
            var jinaUrl = $"{baseUrl}/{url}";

            _logger.LogInformation("Reading webpage with Jina at: {Url}", url);

            // 设置请求头
            var request = new HttpRequestMessage(HttpMethod.Get, jinaUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/markdown"));

            // 如果配置中有 API Key，添加认证头（Jina 高级功能）
            if (!string.IsNullOrEmpty(config.ApiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
            }

            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Jina reader failed with status {StatusCode} for URL: {Url}",
                    response.StatusCode,
                    url);
                return string.Empty;
            }

            var content = await response.Content.ReadAsStringAsync();

            // 截断超长内容防止 Token 溢出
            if (content.Length > MaxContentLength)
            {
                _logger.LogWarning(
                    "Jina content truncated from {OriginalLength} to {MaxLength} characters for URL: {Url}",
                    content.Length,
                    MaxContentLength,
                    url);
                content = content[..MaxContentLength] + "\n\n[内容已截断...]";
            }

            _logger.LogInformation("Successfully read webpage from {Url}, content length: {Length}", url, content.Length);
            return content;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error reading webpage with Jina for URL: {Url}", url);
            return string.Empty;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout reading webpage with Jina for URL: {Url}", url);
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error reading webpage with Jina for URL: {Url}", url);
            return string.Empty;
        }
    }
}
