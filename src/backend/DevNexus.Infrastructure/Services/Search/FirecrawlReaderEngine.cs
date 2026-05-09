using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DevNexus.Core.Abstractions.Search;
using DevNexus.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Search;

/// <summary>
/// Firecrawl API 阅读器实现 - 强大的防爬网页解析
/// 用于处理复杂的 SPA、动态加载、反爬虫页面
/// 文档: https://www.firecrawl.dev/
/// </summary>
public class FirecrawlReaderEngine : IWebReaderEngine
{
    /// <summary>
    /// Firecrawl 官方默认端点
    /// 用户可以在供应商配置中覆盖此地址（支持本地部署或代理）
    /// </summary>
    private const string DefaultFirecrawlApiUrl = "https://api.firecrawl.dev/v1/scrape";
    private const int MaxContentLength = 15000; // 防止超长文本，限制 15000 字符

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FirecrawlReaderEngine> _logger;

    public FirecrawlReaderEngine(IHttpClientFactory httpClientFactory, ILogger<FirecrawlReaderEngine> logger)
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

        if (string.IsNullOrEmpty(config.ApiKey))
        {
            throw new ArgumentException("Firecrawl API Key is required in SearchProvider configuration.");
        }

        try
        {
            // 验证 URL 格式
            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                throw new ArgumentException($"Invalid URL format: {url}");
            }

            var client = _httpClientFactory.CreateClient("Firecrawl");
            
            // 优先从配置中获取 Endpoint，否则使用默认端点
            var apiUrl = string.IsNullOrEmpty(config.Endpoint)
                ? DefaultFirecrawlApiUrl
                : config.Endpoint;
            
            // 构建请求体
            var requestBody = new FirecrawlScrapeRequest
            {
                Url = url,
                Formats = new[] { "markdown" },
                IncludeTags = null, // 可配置包含特定 HTML 标签的内容
                ExcludeTags = new[] { "script", "style", "nav", "footer" }, // 排除无用的脚本和样式
                OnlyMainContent = true, // 只提取主要内容，忽略导航栏等
                WaitFor = null, // 可设置等待选择器出现的时间（毫秒）
            };

            _logger.LogInformation("Reading webpage with Firecrawl at: {Url}", url);

            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
            {
                Content = JsonContent.Create(requestBody),
            };

            // 添加认证头
            request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");

            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Firecrawl scraping failed with status {StatusCode} for URL: {Url}",
                    response.StatusCode,
                    url);
                
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Firecrawl error response: {ErrorContent}", errorContent);
                return string.Empty;
            }

            var result = await response.Content.ReadFromJsonAsync<FirecrawlResponse>();
            if (result?.Success != true || string.IsNullOrEmpty(result.Data?.Markdown))
            {
                _logger.LogWarning("Firecrawl returned invalid response for URL: {Url}", url);
                return string.Empty;
            }

            var content = result.Data.Markdown;

            // 截断超长内容防止 Token 溢出
            if (content.Length > MaxContentLength)
            {
                _logger.LogWarning(
                    "Firecrawl content truncated from {OriginalLength} to {MaxLength} characters for URL: {Url}",
                    content.Length,
                    MaxContentLength,
                    url);
                content = content[..MaxContentLength] + "\n\n[内容已截断...]";
            }

            _logger.LogInformation(
                "Successfully read webpage with Firecrawl from {Url}, content length: {Length}",
                url,
                content.Length);
            return content;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error reading webpage with Firecrawl for URL: {Url}", url);
            return string.Empty;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout reading webpage with Firecrawl for URL: {Url}", url);
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error reading webpage with Firecrawl for URL: {Url}", url);
            return string.Empty;
        }
    }

    private class FirecrawlScrapeRequest
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("formats")]
        public string[]? Formats { get; set; }

        [JsonPropertyName("includeTags")]
        public string[]? IncludeTags { get; set; }

        [JsonPropertyName("excludeTags")]
        public string[]? ExcludeTags { get; set; }

        [JsonPropertyName("onlyMainContent")]
        public bool OnlyMainContent { get; set; }

        [JsonPropertyName("waitFor")]
        public int? WaitFor { get; set; }
    }

    private class FirecrawlResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("data")]
        public FirecrawlData? Data { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    private class FirecrawlData
    {
        [JsonPropertyName("markdown")]
        public string Markdown { get; set; } = string.Empty;

        [JsonPropertyName("html")]
        public string? Html { get; set; }

        [JsonPropertyName("metadata")]
        public FirecrawlMetadata? Metadata { get; set; }
    }

    private class FirecrawlMetadata
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("robots")]
        public string? Robots { get; set; }
    }
}
