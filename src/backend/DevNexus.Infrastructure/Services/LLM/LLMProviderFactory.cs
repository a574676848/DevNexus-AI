// using DevNexus.Domain.Abstractions via GlobalUsings
// using DevNexus.Domain.Configuration via GlobalUsings
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.LLM;

/// <summary>
/// LLM 提供商工厂
/// 从数据库获取配置并创建对应的 LLM 提供商实例
/// </summary>
public class LLMProviderFactory : ILLMProviderFactory
{
    private readonly ILLMProviderManagementService _providerService;
    private readonly IHttpClientFactory _httpClientFactory; // 注入 HttpClient 工厂
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<LLMProviderFactory> _logger;

    private ILLMProvider? _currentProvider;
    private string? _currentModelName;
    private string? _currentProviderName;
    private string? _currentProviderId;
    private Guid? _currentLLMProviderId;
    private string? _currentEndpoint;
    
    // 全局 Singleton 缓存
    private readonly LLMProviderCache _globalCache;
    
    // 当前 Scope 内的局部缓存
    private readonly Dictionary<string, ILLMProvider> _scopedCache = new();

    public LLMProviderFactory(
        ILLMProviderManagementService providerService,
        IEncryptionService encryptionService,
        IHttpClientFactory httpClientFactory, 
        ILoggerFactory loggerFactory,
        LLMProviderCache globalCache)
    {
        _providerService = providerService ?? throw new ArgumentNullException(nameof(providerService));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _globalCache = globalCache ?? throw new ArgumentNullException(nameof(globalCache));
        _logger = loggerFactory.CreateLogger<LLMProviderFactory>();
    }

    /// <summary>
    /// 获取默认提供商
    /// </summary>
    public async Task<ILLMProvider> GetDefaultProviderAsync(CancellationToken cancellationToken = default)
    {
        var providerDto = await _providerService.GetDefaultProviderAsync(cancellationToken);

        if (providerDto == null)
        {
            throw new InvalidOperationException("未配置默认 LLM 供应商，请在数据库中设置");
        }

        return await GetOrCreateProviderAsync(providerDto, cancellationToken);
    }

    /// <summary>
    /// 根据数据库 ID 获取提供商
    /// </summary>
    public async Task<ILLMProvider> GetProviderByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var providerDto = await _providerService.GetProviderByIdAsync(id, cancellationToken);

        if (providerDto == null)
        {
            throw new KeyNotFoundException($"LLM provider not found: {id}");
        }

        return await GetOrCreateProviderAsync(providerDto, cancellationToken);
    }

    /// <summary>
    /// 根据 ProviderId 获取提供商
    /// </summary>
    public async Task<ILLMProvider> GetProviderByProviderIdAsync(string providerId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new ArgumentException("供应商 ID 不能为空", nameof(providerId));
        }

        var providerDto = await _providerService.GetProviderByProviderIdAsync(providerId, cancellationToken);

        if (providerDto == null)
        {
            throw new KeyNotFoundException($"LLM provider not found: {providerId}");
        }

        return await GetOrCreateProviderAsync(providerDto, cancellationToken);
    }

    /// <summary>
    /// 获取当前激活的提供商配置信息
    /// </summary>
    public (string ModelName, string ProviderName, string ProviderId, Guid LLMProviderId, string BaseUrl)? GetCurrentProviderInfo()
    {
        if (_currentModelName == null || _currentProviderName == null || _currentProviderId == null || _currentLLMProviderId == null)
        {
            return null;
        }
        return (_currentModelName, _currentProviderName, _currentProviderId, _currentLLMProviderId.Value, _currentEndpoint ?? string.Empty);
    }

    /// <summary>
    /// 清除缓存
    /// </summary>
    public void InvalidateCache(string? providerId = null)
    {
        if (providerId != null)
        {
            _scopedCache.Remove(providerId); // 清除 Scope
            // 注意：不清除全局 Cache，除非明确知道配置变更（这通常由 ManagementService 触发）
            // 如果需要清除全局，可以使用 _globalCache.Clear() 或暴露 Remove 方法
            // 在这里假设 InvalidateCache 仅用于当前请求上下文的重置
            _logger.LogDebug("[AI.LLM] Invalidated scoped cache for provider: {ProviderId}", providerId);
        }
        else
        {
            _scopedCache.Clear();
            _currentProvider = null;
            _currentModelName = null;
            _currentProviderName = null;
            _currentProviderId = null;
            _currentLLMProviderId = null;
            _currentEndpoint = null;
            _logger.LogDebug("[AI.LLM] Invalidated all scoped provider cache");
        }
    }

    /// <summary>
    /// 获取或创建 Provider 实例
    /// </summary>
    private async Task<ILLMProvider> GetOrCreateProviderAsync(LLMProviderResponse providerDto, CancellationToken cancellationToken)
    {
        // 1. 检查 Scoped Cache (优先)
        var cacheKey = providerDto.ProviderId;
        if (_scopedCache.TryGetValue(cacheKey, out var scopedProvider))
        {
            SetCurrentProviderState(scopedProvider, providerDto);
            return scopedProvider;
        }

        // 2. 检查 Global Singleton Cache
        // 注意：数据库 ID (Guid) 是全局唯一的，适合作为 Singleton Cache Key
        var globalProvider = _globalCache.Get(providerDto.Id);
        if (globalProvider != null)
        {
            // 加入 Scoped Cache 以避免同一请求中重复查 Singleton
            _scopedCache[cacheKey] = globalProvider;
            SetCurrentProviderState(globalProvider, providerDto);
            return globalProvider;
        }

        // 3. 创建新实例 (Cache Miss)
        // 获取解密的 API Key
        var decryptedApiKey = await GetDecryptedApiKeyAsync(providerDto.Id, cancellationToken);

        // 创建配置
        var config = new LLMProviderConfig
        {
            BaseUrl = providerDto.Endpoint,
            Model = providerDto.ModelName,
            ApiKey = decryptedApiKey,
            MaxTokens = GetConfigValue<int>(providerDto.Configuration, "maxTokens", 4096),
            Temperature = GetConfigValue<double>(providerDto.Configuration, "temperature", 0.7),
            TopP = GetConfigValue<double>(providerDto.Configuration, "topP", 0.9),
            GroupId = GetConfigValue<string>(providerDto.Configuration, "groupId", null)
        };

        // 根据 ProviderType 创建对应的 Provider
        var provider = CreateProviderByType(providerDto.Type, config);

        // 4. 存入缓存
        _globalCache.Set(providerDto.Id, provider); // 存入 Singleton
        _scopedCache[cacheKey] = provider;          // 存入 Scoped

        SetCurrentProviderState(provider, providerDto);

        _logger.LogDebug(
            "[AI.LLM] Created provider instance | Type={Type} ProviderId={ProviderId} Model={Model} LLMProviderId={LLMProviderId}",
            providerDto.Type,
            providerDto.ProviderId,
            providerDto.ModelName,
            providerDto.Id);

        return provider;
    }

    private void SetCurrentProviderState(ILLMProvider provider, LLMProviderResponse providerDto)
    {
        _currentProvider = provider;
        _currentModelName = providerDto.ModelName;
        _currentProviderName = providerDto.DisplayName;
        _currentProviderId = providerDto.ProviderId;
        _currentLLMProviderId = providerDto.Id;
        _currentEndpoint = providerDto.Endpoint;
    }

    /// <summary>
    /// 获取解密的 API Key
    /// </summary>
    private async Task<string> GetDecryptedApiKeyAsync(Guid providerId, CancellationToken cancellationToken)
    {
        return await _providerService.GetDecryptedApiKeyAsync(providerId, cancellationToken);
    }

    /// <summary>
    /// 根据 ProviderType 创建对应的 Provider 实例
    /// </summary>
    private ILLMProvider CreateProviderByType(ProviderType type, LLMProviderConfig config)
    {
        return new OpenAICompatibleProvider(
            _httpClientFactory, config, _loggerFactory.CreateLogger<OpenAICompatibleProvider>());
    }

    /// <summary>
    /// 从配置字典获取值
    /// </summary>
    private static T? GetConfigValue<T>(Dictionary<string, object> config, string key, T? defaultValue)
    {
        if (config.TryGetValue(key, out var value))
        {
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }
}
