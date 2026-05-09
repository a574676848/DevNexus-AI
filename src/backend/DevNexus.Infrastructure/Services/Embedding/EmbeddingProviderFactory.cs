// using DevNexus.Domain.Abstractions via GlobalUsings
// using DevNexus.Domain.Configuration via GlobalUsings
using Microsoft.Extensions.Logging;
using DevNexus.Shared.Constants;

namespace DevNexus.Infrastructure.Services.Embedding;

/// <summary>
/// Embedding 提供商工厂
/// 依赖 IEmbeddingProviderManagementService 获取配置，创建和管理 Provider 实例
/// </summary>
public class EmbeddingProviderFactory : IEmbeddingProviderFactory
{
    private readonly IEmbeddingProviderManagementService _managementService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ITokenAuditService _tokenAuditService;
    private readonly Dictionary<Guid, IEmbeddingProvider> _providerCache = new();
    private readonly object _lock = new();

    public EmbeddingProviderFactory(
        IEmbeddingProviderManagementService managementService,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        ITokenAuditService tokenAuditService)
    {
        _managementService = managementService ?? throw new ArgumentNullException(nameof(managementService));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _tokenAuditService = tokenAuditService ?? throw new ArgumentNullException(nameof(tokenAuditService));
    }

    /// <summary>
    /// 获取默认 Embedding 提供商
    /// </summary>
    public IEmbeddingProvider GetDefaultProvider()
    {
        var providerResponse = _managementService.GetDefaultProviderAsync().GetAwaiter().GetResult();
            
        if (providerResponse == null)
        {
            throw new InvalidOperationException("未配置默认 Embedding 供应商");
        }

        return GetOrCreateProvider(providerResponse.Id) ?? throw new InvalidOperationException("创建默认 Embedding 供应商实例失败");
    }

    /// <summary>
    /// 根据 Provider ID 获取特定的 Embedding Provider
    /// </summary>
    public IEmbeddingProvider? GetProvider(Guid providerId)
    {
        lock (_lock)
        {
            if (_providerCache.TryGetValue(providerId, out var cachedProvider))
            {
                return cachedProvider;
            }
        }

        return GetOrCreateProvider(providerId);
    }

    /// <summary>
    /// 根据 Provider ID 获取 Embedding Provider，不存在时抛出异常
    /// </summary>
    public IEmbeddingProvider GetRequiredProvider(Guid providerId)
    {
        var result = GetProvider(providerId);
        if (result == null)
        {
            throw new InvalidOperationException($"Embedding 供应商不存在：{providerId}");
        }
        return result;
    }

    /// <summary>
    /// 获取或创建 Provider 实例（带缓存）
    /// </summary>
    private IEmbeddingProvider? GetOrCreateProvider(Guid providerId)
    {
        lock (_lock)
        {
            if (_providerCache.TryGetValue(providerId, out var cachedProvider))
            {
                return cachedProvider;
            }

            // 获取解密后的凭据
            var credentials = _managementService.GetProviderCredentialsAsync(providerId).GetAwaiter().GetResult();
            if (credentials == null)
            {
                return null;
            }

            var provider = WrapWithAudit(CreateProvider(providerId, credentials));
            _providerCache[providerId] = provider;
            return provider;
        }
    }

    /// <summary>
    /// 根据凭据创建 Provider 实例
    /// </summary>
    private IEmbeddingProvider CreateProvider(Guid providerId, ProviderCredentials credentials)
    {
        // 创建配置对象
        var config = new EmbeddingProviderConfig
        {
            BaseUrl = credentials.Endpoint,
            ApiKey = credentials.ApiKey,
            Model = credentials.ModelName,
            VectorSize = credentials.VectorSize
        };

        // 根据供应商类型创建对应的 Provider 实例
        return credentials.ProviderType switch
        {
            nameof(Shared.Enums.EmbeddingProviderType.Doubao) => new DoubaoEmbeddingProvider(
                _httpClientFactory,
                config,
                _loggerFactory.CreateLogger<DoubaoEmbeddingProvider>(),
                providerId,
                HttpClientNames.DoubaoEmbedding
            ),
            _ => new OpenAIEmbeddingProvider(
                _httpClientFactory,
                config,
                _loggerFactory.CreateLogger<OpenAIEmbeddingProvider>(),
                providerId,
                HttpClientNames.OpenAIEmbedding
            )
        };
    }

    private IEmbeddingProvider WrapWithAudit(IEmbeddingProvider provider)
    {
        return new AuditedEmbeddingProviderDecorator(
            provider,
            _tokenAuditService,
            _loggerFactory.CreateLogger<AuditedEmbeddingProviderDecorator>());
    }

    /// <summary>
    /// 清除缓存
    /// </summary>
    public void ClearCache()
    {
        lock (_lock)
        {
            _providerCache.Clear();
        }
    }

    /// <summary>
    /// 清除特定 Provider 的缓存
    /// </summary>
    public void ClearCache(Guid providerId)
    {
        lock (_lock)
        {
            _providerCache.Remove(providerId);
        }
    }
}
