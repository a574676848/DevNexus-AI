using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DevNexus.Core.Abstractions.Search;
using DevNexus.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Search;

/// <summary>
/// 基于 Tavily 的 AI 精读搜索引擎实现
/// Tavily 直接返回聚合的高质量 raw_content，支持一体化检索
/// 文档：https://tavily.com/
/// </summary>
public class TavilySearchEngine : ISearchEngine
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TavilySearchEngine> _logger;

    public TavilySearchEngine(IHttpClientFactory httpClientFactory, ILogger<TavilySearchEngine> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<List<string>> SearchUrlsAsync(string query, int count, SearchProvider config)
    {
        if (string.IsNullOrEmpty(config.ApiKey))
        {
            throw new ArgumentException("Tavily API Key is not configured.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient("Tavily");
            
            // Tavily API URL（支持自定义端点用于代理）
            var apiUrl = TavilyApiUrlResolver.GetSearchUrl(config.Endpoint);

            var request = new TavilySearchRequest
            {
                ApiKey = config.ApiKey,
                Query = query,
                IncludeRawContent = true,
                MaxResults = count
            };

            _logger.LogInformation("Searching Tavily with query: {Query}", query);

            var response = await client.PostAsJsonAsync(apiUrl, request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                var preview = body[..Math.Min(300, body.Length)];
                throw new HttpRequestException(
                    $"Tavily API request failed with HTTP {(int)response.StatusCode} ({response.StatusCode}) at '{apiUrl}'. Body: {preview}");
            }

            var result = await response.Content.ReadFromJsonAsync<TavilySearchResponse>();
            if (result?.Results == null)
            {
                return new List<string>();
            }

            return result.Results
                .Select(r => r.Url)
                .Where(url => !string.IsNullOrEmpty(url))
                .Take(count)
                .ToList();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling Tavily API");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching with Tavily");
            throw;
        }
    }

    /// <summary>
    /// Tavily 搜索请求模型
    /// </summary>
    private class TavilySearchRequest
    {
        [JsonPropertyName("api_key")]
        public string ApiKey { get; set; } = string.Empty;

        [JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;

        [JsonPropertyName("include_raw_content")]
        public bool IncludeRawContent { get; set; }

        [JsonPropertyName("max_results")]
        public int MaxResults { get; set; } = 5;

        [JsonPropertyName("include_images")]
        public bool IncludeImages { get; set; } = false;
    }

    /// <summary>
    /// Tavily 搜索响应模型
    /// </summary>
    private class TavilySearchResponse
    {
        [JsonPropertyName("results")]
        public List<TavilyResult>? Results { get; set; }
    }

    /// <summary>
    /// Tavily 搜索结果条目
    /// </summary>
    private class TavilyResult
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("raw_content")]
        public string? RawContent { get; set; }
    }
}
