using System.Net.Http.Json;
using DevNexus.Core.Abstractions.Search;
using DevNexus.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Search;

/// <summary>
/// 基于 SearXNG 的元搜索引擎实现
/// </summary>
public class SearXngSearchEngine : ISearchEngine
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SearXngSearchEngine> _logger;

    public SearXngSearchEngine(IHttpClientFactory httpClientFactory, ILogger<SearXngSearchEngine> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<List<string>> SearchUrlsAsync(string query, int count, SearchProvider config)
    {
        if (string.IsNullOrEmpty(config.Endpoint))
        {
            throw new ArgumentException("SearXNG Endpoint is not configured.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient("SearXNG");
            // 构造请求 URL: {Endpoint}/search?q={query}&format=json
            var baseUrl = config.Endpoint.TrimEnd('/');
            var requestUrl = $"{baseUrl}/search?q={Uri.EscapeDataString(query)}&format=json&pageno=1";

            _logger.LogInformation("Searching SearXNG at: {Url}", requestUrl);

            var response = await client.GetFromJsonAsync<SearXngResponse>(requestUrl);
            if (response?.Results == null)
            {
                return new List<string>();
            }

            return response.Results
                .Select(r => r.Url)
                .Where(url => !string.IsNullOrEmpty(url))
                .Take(count)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching SearXNG at {Endpoint}", config.Endpoint);
            return new List<string>();
        }
    }

    private class SearXngResponse
    {
        public List<SearXngResult> Results { get; set; } = new();
    }

    private class SearXngResult
    {
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}
