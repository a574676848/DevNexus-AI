using DevNexus.Core.Abstractions.Search;
using DevNexus.Core.Services.Chat;
using DevNexus.Infrastructure.Services.Search;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;

namespace DevNexus.Infrastructure.Services.Plugins;

/// <summary>
/// 网页搜索插件 (Semantic Kernel Plugin)
/// 支持 Bing Search、Google Custom Search 等多个搜索提供商
/// 从数据库动态读取提供商配置
/// </summary>
public class WebSearchPlugin
{
    private readonly ILogger<WebSearchPlugin> _logger;
    private readonly ISearchProviderManagementService _providerService;
    private readonly IEnumerable<ISearchEngine> _searchEngines;
    private readonly IEnumerable<IWebReaderEngine> _webReaderEngines;

    public WebSearchPlugin(
        ILogger<WebSearchPlugin> logger,
        ISearchProviderManagementService providerService,
        IEnumerable<ISearchEngine> searchEngines,
        IEnumerable<IWebReaderEngine> webReaderEngines)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _providerService = providerService ?? throw new ArgumentNullException(nameof(providerService));
        _searchEngines = searchEngines;
        _webReaderEngines = webReaderEngines ?? throw new ArgumentNullException(nameof(webReaderEngines));
    }

    /// <summary>
    /// 检查并过滤可能由于防爬虫返回的"假成功"内容，以及执行超大文档截断（限制 Token 消耗）
    /// </summary>
    private static bool TryProcessValuableContent(string rawContent, out string processedContent)
    {
        processedContent = string.Empty;
        if (string.IsNullOrWhiteSpace(rawContent)) return false;

        var lowerContent = rawContent.ToLowerInvariant();
        
        // 云服务强力防爬虫特征检测，拦截掉此类"假成功"从而顺利触发下游降级
        if (lowerContent.Contains("please verify you are a human") ||
            (lowerContent.Contains("cloudflare") && lowerContent.Contains("ray id")) ||
            lowerContent.Contains("checking if the site connection is secure"))
        {
            return false;
        }

        // 内容过短通常是对 SPA 抓取纯 JS 失败的情况
        if (rawContent.Length < 200)
        {
            return false;
        }

        const int MaxContentLength = 15000;
        if (rawContent.Length > MaxContentLength)
        {
            processedContent = rawContent.Substring(0, MaxContentLength) + "\n\n... (内容持续过长，为节省 Token 已被系统安全截断)";
        }
        else
        {
            processedContent = rawContent;
        }

        return true;
    }

    [KernelFunction, Description("搜索互联网上的最新信息。返回搜索引擎找到的 URL 和基本信息。")]
    public async Task<string> SearchAsync(
        [Description("搜索关键词")] string query,
        [Description("结果数量")] int count = 5
    )
    {
        await ThinkingContext.EmitAsync($"🔍 正在搜索: {query}...");

        if (string.IsNullOrEmpty(query))
        {
            return JsonSerializer.Serialize(new { success = false, error = "搜索关键词不能为空" });
        }

        try
        {
            // 获取启用的搜索供应商，优先级排序
            var providers = await GetEnabledProvidersAsync();

            if (!providers.Any())
            {
                return JsonSerializer.Serialize(new { success = false, error = "无可用搜索引擎" });
            }

            // 基础事实搜索（只要获取 URL 列表），直接使用 SearXNG 等基础引擎。
            // 强行剥离对最昂贵的 Tavily 的调用，节省 Token 积分并加快返回速度。
            var allUrls = new List<string>();
            var baseProviders = providers.Where(p => p.Type == SearchProviderType.SearXNG).OrderBy(p => p.Priority).ToList();

            foreach (var provider in baseProviders)
            {
                try
                {
                    var engine = CreateSearchEngine(provider);
                    if (engine != null)
                    {
                        var config = await CreateSearchProviderConfigAsync(provider);
                        allUrls = await engine.SearchUrlsAsync(query, count, config);
                        
                        if (allUrls.Any())
                        {
                            _logger.LogInformation("Using {Provider} search engine for query: {Query}", provider.Type, query);
                            break; // 成功取到直接跳出 fallback
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "{Provider} base search failed, trying next", provider.Type);
                }
            }

            if (!allUrls.Any())
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    query = query,
                    error = "未找到任何搜索结果"
                });
            }

            // 转换为 SearchResult 格式供前端渲染（保持向后兼容）
            var results = allUrls.Distinct()
                .Take(count)
                .Select(url => new SearchResult
                {
                    Url = url,
                    Title = ExtractDomainFromUrl(url),
                    Snippet = null,
                    Description = null
                })
                .ToList();

            await ThinkingContext.EmitAsync($"✅ 找到 {results.Count} 条搜索结果");

            return JsonSerializer.Serialize(new
            {
                success = true,
                query = query,
                results = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing search for query: {Query}", query);
            return JsonSerializer.Serialize(new
            {
                success = false,
                query = query,
                error = $"搜索执行失败：{ex.Message}"
            });
        }
    }

    /// <summary>
    /// 高级搜索：获取 URL + 并发读取网页内容 + 合并 Markdown
    /// 实现文档中的"组合策略"：SearXNG -> JinaReader -> Firecrawl 降级
    /// </summary>
    [KernelFunction, Description("执行高级搜索：获取搜索结果 URL，然后并发读取网页内容转为 Markdown。返回完整的合并内容。")]
    public async Task<string> AdvancedSearchAsync(
        [Description("搜索关键词")] string query,
        [Description("结果数量")] int count = 5
    )
    {
        if (string.IsNullOrEmpty(query))
        {
            return JsonSerializer.Serialize(new { success = false, error = "搜索关键词不能为空" });
        }

        try
        {
            // 检查是否启用 Tavily（优先级最高的编排策略）
            var providers = await GetEnabledProvidersAsync();
            var tavilyProvider = providers.FirstOrDefault(p => p.Type == SearchProviderType.Tavily);

            // 如果启用 Tavily，直接走一体化检索（Tavily 已包含高质量 raw_content）
            if (tavilyProvider != null)
            {
                try
                {
                    var tavilyEngine = CreateSearchEngine(tavilyProvider);
                    if (tavilyEngine != null)
                    {
                        var config = await CreateSearchProviderConfigAsync(tavilyProvider);
                        var urls = await tavilyEngine.SearchUrlsAsync(query, count, config);
                        
                        if (urls.Any())
                        {
                            _logger.LogInformation("Using Tavily one-stop search for advanced query: {Query}", query);
                            
                            // Tavily 已经包含了聚合的高质量内容，直接返回
                            var tavilyResults = urls.Select(url => new SearchResult
                            {
                                Url = url,
                                Title = ExtractDomainFromUrl(url),
                                Snippet = "[Tavily AI-aggregated content]",
                                Description = null
                            }).ToList();

                            var tavilyMergedContent = string.Join("\n\n---\n\n", tavilyResults.Select(r =>
                                $"## [{r.Title}]({r.Url})\n\n{r.Snippet}"
                            ));

                            return JsonSerializer.Serialize(new
                            {
                                success = true,
                                query = query,
                                resultCount = tavilyResults.Count,
                                engine = "Tavily",
                                content = tavilyMergedContent,
                                results = tavilyResults
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tavily one-stop search failed, falling back to combined strategy (SearXNG -> JinaReader -> Firecrawl)");
                    // Tavily 失败，继续降级到 SearXNG + 网页读取器的组合策略
                }
            }

            // 降级方案：SearXNG + JinaReader + Firecrawl 组合策略
            // 第一步：通过 SearchAsync 获得 URL 列表（会自动降级到 SearXNG）
            var searchResult = await SearchAsync(query, count);
            var searchResponse = JsonSerializer.Deserialize<JsonElement>(searchResult);
            if (!searchResponse.TryGetProperty("success", out var successElement) || !successElement.GetBoolean())
            {
                return searchResult;
            }

            var allUrls = new List<string>();
            if (searchResponse.TryGetProperty("results", out var resultsElement))
            {
                foreach (var result in resultsElement.EnumerateArray())
                {
                    if (result.TryGetProperty("Url", out var urlElement))
                    {
                        var url = urlElement.GetString();
                        if (!string.IsNullOrEmpty(url))
                        {
                            allUrls.Add(url);
                        }
                    }
                }
            }

            if (!allUrls.Any())
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    query = query,
                    error = "未找到任何搜索结果"
                });
            }

            // 第二步：获取已配置的网页读取器（按优先级）
            var configuredReaders = await GetConfiguredReadersAsync();

            if (!configuredReaders.Any())
            {
                _logger.LogWarning("No web readers configured for advanced search");
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    query = query,
                    error = "未配置网页阅读器，无法进行高级搜索"
                });
            }

            // 第三步：并发读取所有 URL 的内容（使用配置的读取器按优先级降级）
            var tasks = allUrls.Distinct().Take(count).Select(async url =>
            {
                try
                {
                    _logger.LogInformation("Reading content from URL: {Url} using configured readers", url);

                    // 按配置的优先级依次尝试各个读取器
                    foreach (var (readerType, engine, config) in configuredReaders)
                    {
                        try
                        {
                            var rawContent = await engine.ReadWebpageAsync(url, config);

                            if (TryProcessValuableContent(rawContent, out var processedContent))
                            {
                                return new SearchResult
                                {
                                    Url = url,
                                    Title = ExtractDomainFromUrl(url),
                                    Snippet = processedContent,
                                    Description = null
                                };
                            }

                            _logger.LogWarning("{ReaderType} reader returned empty or anti-crawler content for: {Url}", readerType, url);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "{ReaderType} reader failed for URL: {Url}, trying next reader", readerType, url);
                        }
                    }

                    // 所有读取器都失败
                    _logger.LogWarning("All configured readers failed for URL: {Url}", url);
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading content from URL: {Url}", url);
                    return null;
                }
            });

            var results = (await Task.WhenAll(tasks))
                .OfType<SearchResult>()
                .ToList();

            if (!results.Any())
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    query = query,
                    error = "虽然找到了搜索结果，但无法读取任何网页内容"
                });
            }

            // 第四步：合并所有 Markdown 内容
            var mergedContent = string.Join("\n\n---\n\n", results.Select(r =>
                $"## [{r.Url}]({r.Url})\n\n{r.Snippet ?? string.Empty}"
            ));

            return JsonSerializer.Serialize(new
            {
                success = true,
                query = query,
                resultCount = results.Count,
                content = mergedContent,
                results = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing advanced search for query: {Query}", query);
            return JsonSerializer.Serialize(new
            {
                success = false,
                query = query,
                error = $"高级搜索执行失败：{ex.Message}"
            });
        }
    }

    /// <summary>
    /// 读取网页内容转换为 Markdown
    /// 实现分级降级策略：优先使用配置的读取器 -> 自动降级到备选方案
    /// </summary>
    [KernelFunction, Description("读取指定 URL 的网页内容，转换为可用的 Markdown 格式。系统会自动选择最优阅读器。")]
    public async Task<string> ReadWebpageAsync(
        [Description("网页 URL")] string url,
        [Description("读取方式，可选：auto(自动)、jina、firecrawl")] string method = "auto"
    )
    {
        await ThinkingContext.EmitAsync($"🌐 正在读取网页: {url}...");

        if (string.IsNullOrEmpty(url))
        {
            return JsonSerializer.Serialize(new { success = false, error = "URL 不能为空" });
        }

        // 验证 URL 格式
        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            return JsonSerializer.Serialize(new { success = false, error = $"无效的 URL 格式：{url}" });
        }

        if (WebResourceRoutingPolicy.IsGitRepositoryUrl(url))
        {
            _logger.LogInformation("Blocked Git repository URL from webpage readers: {Url}", url);
            return JsonSerializer.Serialize(new
            {
                success = false,
                url,
                error = WebResourceRoutingPolicy.GitRepositoryReaderError,
                recommendedSkill = "repo-parser"
            });
        }

        try
        {
            // 获取已配置的网页读取器
            var readers = await GetConfiguredReadersAsync();

            if (!readers.Any())
            {
                _logger.LogWarning("No web reader engines configured");
                return JsonSerializer.Serialize(new { success = false, error = "无可用网页阅读器配置" });
            }

            string content = string.Empty;
            string usedMethod = "unknown";

            // 分级降级策略
            foreach (var (readerType, engine, config) in readers)
            {
                if (method != "auto" && !method.Equals(readerType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    _logger.LogInformation("Attempting to read webpage with {ReaderType} for URL: {Url}", readerType, url);
                    var rawContent = await engine.ReadWebpageAsync(url, config);

                    if (TryProcessValuableContent(rawContent, out var processedContent))
                    {
                        usedMethod = readerType;
                        await ThinkingContext.EmitAsync("✅ 网页内容已提取");
                        return JsonSerializer.Serialize(new
                        {
                            success = true,
                            url = url,
                            method = usedMethod,
                            content = processedContent
                        });
                    }

                    _logger.LogWarning("{ReaderType} reader failed or returned anti-crawler content for: {Url}", readerType, url);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "{ReaderType} reader error for URL: {Url}", readerType, url);
                }
            }

            // 所有策略都失败
            _logger.LogError("All webpage reading strategies failed for: {Url}", url);
            return JsonSerializer.Serialize(new
            {
                success = false,
                url = url,
                error = "无法读取网页内容。请检查 URL 是否有效、网络连接，或尝试稍后再试。"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading webpage: {Url}", url);
            return JsonSerializer.Serialize(new
            {
                success = false,
                url = url,
                error = $"读取网页时出错：{ex.Message}"
            });
        }
    }

    /// <summary>
    /// 获取已配置的网页读取器列表（优先级排序）
    /// 从 SearchProvider 中提取类型为 JinaReader 或 Firecrawl 的配置
    /// </summary>
    private async Task<List<(string Type, IWebReaderEngine Engine, DevNexus.Domain.Entities.SearchProvider Config)>> GetConfiguredReadersAsync()
    {
        var result = new List<(string, IWebReaderEngine, DevNexus.Domain.Entities.SearchProvider)>();

        try
        {
            var providers = (await _providerService.GetAllProvidersAsync(false))
                .Where(p => p.IsEnabled &&
                       (p.Type == SearchProviderType.JinaReader || p.Type == SearchProviderType.Firecrawl))
                .OrderBy(p => p.Priority)
                .ToList();

            foreach (var provider in providers)
            {
                var engine = CreateWebReaderEngine(provider);
                if (engine == null) continue;

                var config = await CreateSearchProviderConfigAsync(provider);
                var typeName = GetReaderTypeName(provider.Type);

                result.Add((typeName, engine, config));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configured web readers");
        }

        return result;
    }

    /// <summary>
    /// 根据供应商类型创建网页读取器实例
    /// </summary>
    private IWebReaderEngine? CreateWebReaderEngine(DevNexus.Shared.DTOs.SearchProviderResponse provider)
    {
        return provider.Type switch
        {
            SearchProviderType.JinaReader => _webReaderEngines.OfType<JinaReaderEngine>().FirstOrDefault(),
            SearchProviderType.Firecrawl => _webReaderEngines.OfType<FirecrawlReaderEngine>().FirstOrDefault(),
            _ => null
        };
    }

    /// <summary>
    /// 获取网页读取器的类型名称（用于日志和返回值）
    /// </summary>
    private string GetReaderTypeName(SearchProviderType type)
    {
        return type switch
        {
            SearchProviderType.JinaReader => "jina",
            SearchProviderType.Firecrawl => "firecrawl",
            _ => "unknown"
        };
    }

    /// <summary>
    /// 搜索结果条目 - 包含 URL、标题、网页内容等信息
    /// </summary>
    private class SearchResult
    {
        /// <summary>
        /// 源 URL
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// 网页标题或域名
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 网页转换后的 Markdown 内容（用于高级搜索）
        /// </summary>
        public string? Snippet { get; set; }

        /// <summary>
        /// 网页描述或摘要（可选）
        /// </summary>
        public string? Description { get; set; }
    }

    /// <summary>
    /// 获取所有启用的供应商配置（按优先级排序）
    /// </summary>
    private async Task<List<DevNexus.Shared.DTOs.SearchProviderResponse>> GetEnabledProvidersAsync()
    {
        try
        {
            return (await _providerService.GetAllProvidersAsync(false))
                .Where(p => p.IsEnabled)
                .OrderBy(p => p.Priority)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving enabled providers");
            return new List<DevNexus.Shared.DTOs.SearchProviderResponse>();
        }
    }

    /// <summary>
    /// 根据供应商类型创建搜索引擎实例
    /// </summary>
    private ISearchEngine? CreateSearchEngine(DevNexus.Shared.DTOs.SearchProviderResponse provider)
    {
        return provider.Type switch
        {
            SearchProviderType.SearXNG => _searchEngines.OfType<SearXngSearchEngine>().FirstOrDefault(),
            SearchProviderType.Tavily => _searchEngines.OfType<TavilySearchEngine>().FirstOrDefault(),
            _ => null
        };
    }

    /// <summary>
    /// 根据供应商创建搜索提供商配置对象（包含 API Key）
    /// </summary>
    private async Task<DevNexus.Domain.Entities.SearchProvider> CreateSearchProviderConfigAsync(
        DevNexus.Shared.DTOs.SearchProviderResponse provider)
    {
        var apiKey = await _providerService.GetDecryptedApiKeyAsync(provider.Id);
        return new DevNexus.Domain.Entities.SearchProvider
        {
            Endpoint = provider.Endpoint,
            ApiKey = apiKey
        };
    }

    /// <summary>
    /// 从 URL 中提取域名作为标题
    /// </summary>
    private string ExtractDomainFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host;
        }
        catch
        {
            return url;
        }
    }
}
