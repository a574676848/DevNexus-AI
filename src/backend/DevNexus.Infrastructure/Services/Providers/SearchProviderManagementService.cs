using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
// using DevNexus.Domain.Abstractions via GlobalUsings
// using DevNexus.Domain.Entities via GlobalUsings
using DevNexus.Infrastructure.Models;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using DevNexus.Infrastructure.Services.Search;

namespace DevNexus.Infrastructure.Services.Providers;

/// <summary>
/// 搜索供应商管理服务实现
/// </summary>
public class SearchProviderManagementService : ISearchProviderManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDistributedCache _cache;
    private readonly ILogger<SearchProviderManagementService> _logger;

    private const string CacheKeyPrefix = "search_providers:";
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromHours(1);

    public SearchProviderManagementService(
        ApplicationDbContext context,
        IEncryptionService encryptionService,
        IHttpClientFactory httpClientFactory,
        IDistributedCache cache,
        ILogger<SearchProviderManagementService> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }
    
    public async Task<IEnumerable<SearchProviderResponse>> GetAllProvidersAsync(
        bool includeDisabled = false,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{CacheKeyPrefix}all:{(includeDisabled ? "inc" : "exc")}";
        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cached))
        {
            return JsonSerializer.Deserialize<IEnumerable<SearchProviderResponse>>(cached)!;
        }

        var query = _context.SearchProviders.AsQueryable();
        
        if (!includeDisabled)
        {
            query = query.Where(p => p.IsEnabled);
        }
        
        var providers = await query
            .OrderBy(p => p.Priority)
            .ThenBy(p => p.DisplayName)
            .ToListAsync(cancellationToken);
        
        var results = providers.Select(MapToResponse).ToList();
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(results), 
            new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheExpiry }, 
            cancellationToken);

        return results;
    }
    
    public async Task<SearchProviderResponse?> GetProviderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.SearchProviders.FindAsync(
            new object[] { id },
            cancellationToken);
            
        return provider == null ? null : MapToResponse(provider);
    }
    
    public async Task<SearchProviderResponse?> GetProviderByProviderIdAsync(
        string providerId,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.SearchProviders
            .FirstOrDefaultAsync(p => p.ProviderId == providerId, cancellationToken);
            
        return provider == null ? null : MapToResponse(provider);
    }
    
    public async Task<SearchProviderResponse?> GetDefaultProviderAsync(
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.SearchProviders
            .Where(p => p.IsEnabled && p.IsDefault)
            .OrderBy(p => p.Priority)
            .FirstOrDefaultAsync(cancellationToken);
            
        return provider == null ? null : MapToResponse(provider);
    }
    
    public async Task<SearchProviderResponse> CreateProviderAsync(
        CreateSearchProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        
        var provider = new SearchProvider
        {
            ProviderId = request.ProviderId,
            DisplayName = request.DisplayName,
            Type = request.Type,
            LogoUrl = request.LogoUrl,
            Endpoint = request.Endpoint,
            ApiKey = _encryptionService.Encrypt(request.ApiKey), // 加密存储
            IsEnabled = request.IsEnabled,
            IsDefault = request.IsDefault,
            Priority = request.Priority,
            Configuration = request.Configuration ?? new(),
            SearchEngineId = request.SearchEngineId
        };
        
        // 如果设置为默认,取消其他默认
        if (provider.IsDefault)
        {
            await UnsetAllDefaultsAsync(cancellationToken);
        }
        
        _context.SearchProviders.Add(provider);
        await _context.SaveChangesAsync(cancellationToken);
        
        await InvalidateCacheAsync(cancellationToken);

        _logger.LogDebug(
            "Created Search provider: {ProviderId} (ID: {Id})",
            provider.ProviderId,
            provider.Id);
        
        return MapToResponse(provider);
    }
    
    public async Task<SearchProviderResponse> UpdateProviderAsync(
        Guid id,
        UpdateSearchProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.SearchProviders.FindAsync(
            new object[] { id },
            cancellationToken);
            
        if (provider == null)
        {
            throw new KeyNotFoundException($"Provider not found: {id}");
        }
        
        // 更新字段
        if (request.DisplayName != null)
            provider.DisplayName = request.DisplayName;
        if (request.LogoUrl != null)
            provider.LogoUrl = request.LogoUrl;
        if (request.Endpoint != null)
            provider.Endpoint = request.Endpoint;
        if (request.ApiKey != null)
            provider.ApiKey = _encryptionService.Encrypt(request.ApiKey);
        if (request.IsEnabled.HasValue)
            provider.IsEnabled = request.IsEnabled.Value;
        if (request.Priority.HasValue)
            provider.Priority = request.Priority.Value;
        if (request.Configuration != null)
            provider.Configuration = request.Configuration;
        if (request.SearchEngineId != null)
            provider.SearchEngineId = request.SearchEngineId;
        
        provider.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync(cancellationToken);
        await InvalidateCacheAsync(cancellationToken);
        
        _logger.LogDebug(
            "Updated Search provider: {ProviderId} (ID: {Id})",
            provider.ProviderId,
            provider.Id);
        
        return MapToResponse(provider);
    }
    
    public async Task<bool> DeleteProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.SearchProviders.FindAsync(
            new object[] { id },
            cancellationToken);
            
        if (provider == null)
        {
            return false;
        }
        
        _context.SearchProviders.Remove(provider);
        await _context.SaveChangesAsync(cancellationToken);
        await InvalidateCacheAsync(cancellationToken);
        
        _logger.LogDebug(
            "Deleted Search provider: {ProviderId} (ID: {Id})",
            provider.ProviderId,
            provider.Id);
        
        return true;
    }

    public async Task<ValidateProviderResponse> TestProviderConnectionAsync(
        CreateSearchProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return request.Type switch
            {
                SearchProviderType.SearXNG  => await TestSearXNGAsync(request, cancellationToken),
                SearchProviderType.Tavily   => await TestTavilyAsync(request, cancellationToken),
                SearchProviderType.JinaReader => await TestJinaReaderAsync(request, cancellationToken),
                SearchProviderType.Firecrawl  => await TestFirecrawlAsync(request, cancellationToken),
                _ => new ValidateProviderResponse { IsValid = false, ErrorMessage = "不支持的搜索引擎类型" }
            };
        }
        catch (TaskCanceledException)
        {
            return new ValidateProviderResponse { IsValid = false, ErrorMessage = "连接超时，请检查服务地址是否可达" };
        }
        catch (HttpRequestException ex)
        {
            return new ValidateProviderResponse { IsValid = false, ErrorMessage = $"网络请求失败: {ex.Message}" };
        }
        catch (Exception ex)
        {
            return new ValidateProviderResponse { IsValid = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// 测试 SearXNG 连接 - 调用元数据接口验证服务可用性
    /// </summary>
    private async Task<ValidateProviderResponse> TestSearXNGAsync(
        CreateSearchProviderRequest request,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);

        var baseUrl = request.Endpoint.TrimEnd('/');
        // 如果配置了 API Key，加入请求头
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {request.ApiKey}");
        }

        // SearXNG 提供元信息接口，返回实例信息即代表服务正常
        var response = await httpClient.GetAsync($"{baseUrl}/", cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return new ValidateProviderResponse { IsValid = true };
        }

        // 若根路径不通，尝试 /search 接口探测
        var searchUrl = $"{baseUrl}/search?q=test&format=json";
        response = await httpClient.GetAsync(searchUrl, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return new ValidateProviderResponse { IsValid = true };
        }

        return new ValidateProviderResponse
        {
            IsValid = false,
            ErrorMessage = $"SearXNG 服务无响应 (HTTP {(int)response.StatusCode})，请检查 Endpoint 地址"
        };
    }

    /// <summary>
    /// 测试 Tavily 连接 - 使用最小搜索请求验证 API Key
    /// </summary>
    private async Task<ValidateProviderResponse> TestTavilyAsync(
        CreateSearchProviderRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return new ValidateProviderResponse { IsValid = false, ErrorMessage = "Tavily 需要填写 API Key" };
        }

        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(15);

        var searchUrl = TavilyApiUrlResolver.GetSearchUrl(request.Endpoint);

        // Tavily 搜索请求体（最小化，max_results=1 减少费用消耗）
        var payload = new
        {
            api_key = request.ApiKey,
            query = "test",
            max_results = 1
        };

        using var content = System.Net.Http.Json.JsonContent.Create(payload);
        var response = await httpClient.PostAsync(searchUrl, content, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new ValidateProviderResponse { IsValid = true };
        }

        // 401/403 表示 API Key 无效
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
            response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            return new ValidateProviderResponse { IsValid = false, ErrorMessage = "API Key 无效或已过期" };
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return new ValidateProviderResponse
        {
            IsValid = false,
            ErrorMessage = $"Tavily 返回错误 (HTTP {(int)response.StatusCode}): {body[..Math.Min(200, body.Length)]}"
        };
    }

    /// <summary>
    /// 测试 Jina Reader 连接 - 抓取 example.com 验证 Token 可用性
    /// </summary>
    private async Task<ValidateProviderResponse> TestJinaReaderAsync(
        CreateSearchProviderRequest request,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(15);
        httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        var baseUrl = string.IsNullOrWhiteSpace(request.Endpoint)
            ? "https://r.jina.ai"
            : request.Endpoint.TrimEnd('/');

        // Jina Reader 不支持 HEAD 请求 (会返回 HTTP 405)，因此必须使用 GET 请求
        // 抓取一个极简网页 (example.com) 以尽量减少免费额度消耗
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/https://example.com");
        
        // 只有当有 Key 时才添加授权头
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            requestMessage.Headers.Add("Authorization", $"Bearer {request.ApiKey}");
        }
        requestMessage.Headers.Add("Accept", "application/json");

        var response = await httpClient.SendAsync(requestMessage, cancellationToken);

        return response.StatusCode switch
        {
            // 2xx: Key 有效，抓取成功
            _ when response.IsSuccessStatusCode
                => new ValidateProviderResponse { IsValid = true },

            // 402: Key 有效但账户余额不足（需充值），Key 本身是正确的
            System.Net.HttpStatusCode.PaymentRequired
                => new ValidateProviderResponse { IsValid = true, ErrorMessage = "API Key 有效（账户余额不足，部分功能受限）" },

            // 401: Token 无效或未提供
            System.Net.HttpStatusCode.Unauthorized
                => new ValidateProviderResponse { IsValid = false, ErrorMessage = "Jina Reader API Key 无效，请检查 Key 是否正确" },

            // 403: 无权限访问（Key 正确但无此权限）
            System.Net.HttpStatusCode.Forbidden
                => new ValidateProviderResponse { IsValid = false, ErrorMessage = "Jina Reader 拒绝访问（403 Forbidden），账户可能无此API权限" },

            // 其他错误
            _ => new ValidateProviderResponse
            {
                IsValid = false,
                ErrorMessage = $"Jina Reader 服务异常 (HTTP {(int)response.StatusCode})"
            }
        };
    }

    /// <summary>
    /// 测试 Firecrawl 连接 - 调用 /v1/scrape 端点验证 API Key
    /// </summary>
    private async Task<ValidateProviderResponse> TestFirecrawlAsync(
        CreateSearchProviderRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return new ValidateProviderResponse { IsValid = false, ErrorMessage = "Firecrawl 需要填写 API Key" };
        }

        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(15);
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {request.ApiKey}");

        var baseUrl = string.IsNullOrWhiteSpace(request.Endpoint)
            ? "https://api.firecrawl.dev"
            : request.Endpoint.TrimEnd('/');

        // 使用最小请求体调用 /v1/scrape 端点
        var payload = new { url = "https://example.com", formats = new[] { "markdown" } };
        using var content = System.Net.Http.Json.JsonContent.Create(payload);
        var response = await httpClient.PostAsync($"{baseUrl}/v1/scrape", content, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new ValidateProviderResponse { IsValid = true };
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return new ValidateProviderResponse { IsValid = false, ErrorMessage = "Firecrawl API Key 无效" };
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return new ValidateProviderResponse
        {
            IsValid = false,
            ErrorMessage = $"Firecrawl 返回错误 (HTTP {(int)response.StatusCode}): {body[..Math.Min(200, body.Length)]}"
        };
    }
    
    public async Task<SearchProviderResponse> SetDefaultProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        // 此方法已废弃，改为多选启用模式。为了保持接口兼容，抛出异常或简单记录日志
        throw new NotSupportedException("SetDefaultProviderAsync is no longer supported. Please use IsEnabled instead.");
    }
    
    public async Task<ValidateProviderResponse> ValidateProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.SearchProviders.FindAsync(
            new object[] { id },
            cancellationToken);

        if (provider == null)
        {
            throw new KeyNotFoundException($"Provider not found: {id}");
        }

        // 解密存储的 API Key
        var apiKey = _encryptionService.Decrypt(provider.ApiKey);

        // 构造临时的测试请求，复用真实的测试逻辑
        var testRequest = new CreateSearchProviderRequest
        {
            Type     = provider.Type,
            Endpoint = provider.Endpoint,
            ApiKey   = apiKey
        };

        var result = await TestProviderConnectionAsync(testRequest, cancellationToken);

        // 将验证结果写回数据库
        provider.ValidationStatus  = result.IsValid ? ValidationStatus.Valid : ValidationStatus.Invalid;
        provider.ValidationError   = result.IsValid ? null : result.ErrorMessage;
        provider.LastValidatedAt   = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Search provider {ProviderId} validation result: {IsValid}, Error: {Error}",
            provider.ProviderId, result.IsValid, result.ErrorMessage);

        return result;
    }
    
    #region Public Helper Methods
    
    /// <summary>
    /// 获取解密后的 API Key（仅供内部服务使用）
    /// </summary>
    public async Task<string> GetDecryptedApiKeyAsync(Guid providerId)
    {
        var provider = await _context.SearchProviders.FindAsync(providerId);
        if (provider == null)
        {
            throw new KeyNotFoundException($"Provider not found: {providerId}");
        }
        
        return _encryptionService.Decrypt(provider.ApiKey);
    }
    
    #endregion
    
    #region Private Methods
    
    private async Task UnsetAllDefaultsAsync(CancellationToken cancellationToken)
    {
        var currentDefaults = await _context.SearchProviders
            .Where(p => p.IsDefault)
            .ToListAsync(cancellationToken);
            
        foreach (var provider in currentDefaults)
        {
            provider.IsDefault = false;
        }
    }
    
    private SearchProviderResponse MapToResponse(SearchProvider provider)
    {
        return new SearchProviderResponse
        {
            Id = provider.Id,
            ProviderId = provider.ProviderId,
            DisplayName = provider.DisplayName,
            Type = provider.Type,
            LogoUrl = provider.LogoUrl,
            Endpoint = provider.Endpoint,
            IsEnabled = provider.IsEnabled,
            IsDefault = provider.IsDefault,
            Priority = provider.Priority,
            Configuration = provider.Configuration,
            LastValidatedAt = provider.LastValidatedAt,
            ValidationStatus = provider.ValidationStatus,
            ValidationError = provider.ValidationError,
            SearchEngineId = provider.SearchEngineId,
            CreatedAt = provider.CreatedAt,
            UpdatedAt = provider.UpdatedAt
        };
    }
    
    
    private Task<(bool IsValid, string? ErrorMessage)> ValidateDuckDuckGoAsync(
        SearchProvider provider,
        string apiKey,
        CancellationToken cancellationToken)
    {
        // DuckDuckGo 通常不需要 API Key
        return Task.FromResult<(bool, string?)>((true, null));
    }
    
    private async Task InvalidateCacheAsync(CancellationToken cancellationToken)
    {
        await _cache.RemoveAsync($"{CacheKeyPrefix}all:inc", cancellationToken);
        await _cache.RemoveAsync($"{CacheKeyPrefix}all:exc", cancellationToken);
    }

    #endregion
}
