// using DevNexus.Domain.Abstractions via GlobalUsings
// using DevNexus.Domain.Entities via GlobalUsings
using DevNexus.Infrastructure.Models;
using DevNexus.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DevNexus.Infrastructure.Services.Providers;

/// <summary>
/// 模型定价管理服务实现
/// </summary>
public class ModelPricingService : IModelPricingService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ModelPricingService> _logger;

    private const string CacheKeyPrefix = "model-pricing:";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(1);

    public ModelPricingService(
        ApplicationDbContext dbContext,
        IDistributedCache cache,
        ILogger<ModelPricingService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ModelPricingResponse>> GetAllPricingsAsync(
        CancellationToken cancellationToken = default)
    {
        var pricings = await _dbContext.ModelPrices
            .OrderBy(mp => mp.ProviderType)
            .ThenBy(mp => mp.ProviderId)
            .ToListAsync(cancellationToken);

        return await MapToResponsesAsync(pricings, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ModelPricingResponse?> GetPricingByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var pricing = await _dbContext.ModelPrices
            .FirstOrDefaultAsync(mp => mp.Id == id, cancellationToken);

        if (pricing == null)
        {
            return null;
        }

        return await MapToResponseAsync(pricing, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ModelPricingResponse?> GetPricingByProviderIdAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        return await GetPricingByProviderAsync(
            ModelInvocationProviderTypes.Llm,
            providerId,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ModelPricingResponse?> GetPricingByProviderAsync(
        string providerType,
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{CacheKeyPrefix}{providerType}:provider:{providerId}";

        var cachedData = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedData))
        {
            try
            {
                return JsonSerializer.Deserialize<ModelPricingResponse>(cachedData);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "[ModelPricing] Failed to deserialize cached pricing for provider {ProviderId}", providerId);
            }
        }

        var pricing = await _dbContext.ModelPrices
            .FirstOrDefaultAsync(
                mp => mp.ProviderType == providerType && mp.ProviderId == providerId && mp.IsEnabled,
                cancellationToken);
        if (pricing == null)
        {
            return null;
        }

        var response = await MapToResponseAsync(pricing, cancellationToken);

        if (response == null)
        {
            return null;
        }

        await SetCacheAsync(cacheKey, response, cancellationToken);

        return response;
    }

    /// <inheritdoc />
    public async Task<ModelPricingResponse?> GetPricingByProviderProviderIdAsync(
        string providerProviderId,
        CancellationToken cancellationToken = default)
    {
        return await GetPricingByProviderProviderIdAsync(
            ModelInvocationProviderTypes.Llm,
            providerProviderId,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ModelPricingResponse?> GetPricingByProviderProviderIdAsync(
        string providerType,
        string providerProviderId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{CacheKeyPrefix}{providerType}:provider-id:{providerProviderId}";

        var cachedData = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedData))
        {
            try
            {
                return JsonSerializer.Deserialize<ModelPricingResponse>(cachedData);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "[ModelPricing] Failed to deserialize cached pricing for provider {ProviderProviderId}", providerProviderId);
            }
        }

        var pricing = await FindPricingByProviderCodeAsync(providerType, providerProviderId, cancellationToken);
        if (pricing == null)
        {
            return null;
        }

        var response = await MapToResponseAsync(pricing, cancellationToken);

        if (response == null)
        {
            return null;
        }

        await SetCacheAsync(cacheKey, response, cancellationToken);

        return response;
    }

    /// <inheritdoc />
    public async Task<ModelPricingResponse> CreatePricingAsync(
        CreateModelPricingRequest request,
        CancellationToken cancellationToken = default)
    {
        var providerDisplayName = await ValidateProviderExistsAsync(
            request.ProviderType,
            request.ProviderId,
            cancellationToken);

        var existing = await _dbContext.ModelPrices
            .FirstOrDefaultAsync(
                mp => mp.ProviderType == request.ProviderType && mp.ProviderId == request.ProviderId,
                cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException($"Pricing for provider {providerDisplayName} already exists.");
        }

        var pricing = new ModelPricing
        {
            ProviderType = request.ProviderType,
            ProviderId = request.ProviderId,
            InputCostPerMillion = request.InputCostPerMillion,
            OutputCostPerMillion = request.OutputCostPerMillion,
            Currency = request.Currency,
            IsEnabled = request.IsEnabled
        };

        await _dbContext.ModelPrices.AddAsync(pricing, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[ModelPricing] Created pricing for provider {ProviderName} | ProviderType={ProviderType}",
            providerDisplayName,
            request.ProviderType);
        return await MapToResponseAsync(pricing, cancellationToken)
            ?? throw new InvalidOperationException("关联供应商不存在或已删除，无法返回定价配置。");
    }

    /// <inheritdoc />
    public async Task<ModelPricingResponse> UpdatePricingAsync(
        Guid id,
        UpdateModelPricingRequest request,
        CancellationToken cancellationToken = default)
    {
        var pricing = await _dbContext.ModelPrices
            .FirstOrDefaultAsync(mp => mp.Id == id, cancellationToken);

        if (pricing == null)
        {
            throw new KeyNotFoundException($"Model pricing with ID {id} not found.");
        }

        pricing.ProviderType = request.ProviderType;
        pricing.InputCostPerMillion = request.InputCostPerMillion;
        pricing.OutputCostPerMillion = request.OutputCostPerMillion;
        pricing.Currency = request.Currency;
        pricing.IsEnabled = request.IsEnabled;
        pricing.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // 失效缓存
        var providerCode = await GetProviderCodeAsync(pricing.ProviderType, pricing.ProviderId, cancellationToken);
        await InvalidateCacheAsync(pricing.ProviderType, pricing.ProviderId, providerCode, cancellationToken);
        _logger.LogInformation(
            "[ModelPricing] Updated pricing {Id} | ProviderType={ProviderType}",
            id,
            pricing.ProviderType);
        return await MapToResponseAsync(pricing, cancellationToken)
            ?? throw new InvalidOperationException("关联供应商不存在或已删除，无法返回定价配置。");
    }

    /// <inheritdoc />
    public async Task<bool> DeletePricingAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var pricing = await _dbContext.ModelPrices
            .FirstOrDefaultAsync(mp => mp.Id == id, cancellationToken);

        if (pricing == null)
        {
            return false;
        }

        var providerType = pricing.ProviderType;
        var providerId = pricing.ProviderId;
        var providerProviderId = await GetProviderCodeAsync(providerType, providerId, cancellationToken);

        _dbContext.ModelPrices.Remove(pricing);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await InvalidateCacheAsync(providerType, providerId, providerProviderId, cancellationToken);
        _logger.LogInformation("[ModelPricing] Deleted pricing {Id}", id);

        return true;
    }

    /// <summary>
    /// 映射实体到响应 DTO
    /// </summary>
    private async Task<List<ModelPricingResponse>> MapToResponsesAsync(
        IReadOnlyCollection<ModelPricing> pricings,
        CancellationToken cancellationToken)
    {
        var llmIds = pricings
            .Where(item => item.ProviderType == ModelInvocationProviderTypes.Llm)
            .Select(item => item.ProviderId)
            .Distinct()
            .ToArray();
        var embeddingIds = pricings
            .Where(item => item.ProviderType == ModelInvocationProviderTypes.Embedding)
            .Select(item => item.ProviderId)
            .Distinct()
            .ToArray();

        var llmMap = llmIds.Length == 0
            ? new Dictionary<Guid, LLMProvider>()
            : await _dbContext.LLMProviders
                .Where(item => llmIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
        var embeddingMap = embeddingIds.Length == 0
            ? new Dictionary<Guid, EmbeddingProvider>()
            : await _dbContext.EmbeddingProviders
                .Where(item => embeddingIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);

        // 供应商已被软删除时，对应定价项不再对外展示，避免管理列表出现脏数据。
        return pricings
            .Select(item => MapToResponse(item, llmMap, embeddingMap))
            .OfType<ModelPricingResponse>()
            .ToList();
    }

    private async Task<ModelPricingResponse?> MapToResponseAsync(
        ModelPricing pricing,
        CancellationToken cancellationToken)
    {
        var responses = await MapToResponsesAsync(new[] { pricing }, cancellationToken);
        return responses.FirstOrDefault();
    }

    private static ModelPricingResponse? MapToResponse(
        ModelPricing pricing,
        IReadOnlyDictionary<Guid, LLMProvider> llmMap,
        IReadOnlyDictionary<Guid, EmbeddingProvider> embeddingMap)
    {
        if (pricing.ProviderType == ModelInvocationProviderTypes.Llm &&
            llmMap.TryGetValue(pricing.ProviderId, out var llmProvider))
        {
            return new ModelPricingResponse
            {
                Id = pricing.Id,
                ProviderType = pricing.ProviderType,
                ProviderId = pricing.ProviderId,
                ProviderDisplayName = llmProvider.DisplayName,
                ProviderProviderId = llmProvider.ProviderId,
                InputCostPerMillion = pricing.InputCostPerMillion,
                OutputCostPerMillion = pricing.OutputCostPerMillion,
                Currency = pricing.Currency,
                IsEnabled = pricing.IsEnabled,
                CreatedAt = pricing.CreatedAt,
                UpdatedAt = pricing.UpdatedAt
            };
        }

        if (pricing.ProviderType == ModelInvocationProviderTypes.Embedding &&
            embeddingMap.TryGetValue(pricing.ProviderId, out var embeddingProvider))
        {
            return new ModelPricingResponse
            {
                Id = pricing.Id,
                ProviderType = pricing.ProviderType,
                ProviderId = pricing.ProviderId,
                ProviderDisplayName = embeddingProvider.DisplayName,
                ProviderProviderId = embeddingProvider.ProviderId,
                InputCostPerMillion = pricing.InputCostPerMillion,
                OutputCostPerMillion = pricing.OutputCostPerMillion,
                Currency = pricing.Currency,
                IsEnabled = pricing.IsEnabled,
                CreatedAt = pricing.CreatedAt,
                UpdatedAt = pricing.UpdatedAt
            };
        }

        return null;
    }

    /// <summary>
    /// 写入缓存
    /// </summary>
    private async Task SetCacheAsync(
        string cacheKey,
        ModelPricingResponse response,
        CancellationToken cancellationToken)
    {
        try
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheExpiration
            };
            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(response),
                options,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ModelPricing] Failed to set cache for key {CacheKey}", cacheKey);
        }
    }

    /// <summary>
    /// 失效缓存
    /// </summary>
    private async Task InvalidateCacheAsync(
        string providerType,
        Guid providerId,
        string? providerProviderId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _cache.RemoveAsync($"{CacheKeyPrefix}{providerType}:provider:{providerId}", cancellationToken);
            if (!string.IsNullOrEmpty(providerProviderId))
            {
                await _cache.RemoveAsync($"{CacheKeyPrefix}{providerType}:provider-id:{providerProviderId}", cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ModelPricing] Failed to invalidate cache for provider {ProviderId}", providerId);
        }
    }

    private async Task<string> ValidateProviderExistsAsync(
        string providerType,
        Guid providerId,
        CancellationToken cancellationToken)
    {
        if (providerType == ModelInvocationProviderTypes.Llm)
        {
            var provider = await _dbContext.LLMProviders.FirstOrDefaultAsync(p => p.Id == providerId, cancellationToken);
            if (provider == null)
            {
                throw new ArgumentException($"LLM Provider with ID {providerId} not found.");
            }

            return provider.DisplayName;
        }

        if (providerType == ModelInvocationProviderTypes.Embedding)
        {
            var provider = await _dbContext.EmbeddingProviders.FirstOrDefaultAsync(p => p.Id == providerId, cancellationToken);
            if (provider == null)
            {
                throw new ArgumentException($"Embedding Provider with ID {providerId} not found.");
            }

            return provider.DisplayName;
        }

        throw new ArgumentException($"Unsupported provider type: {providerType}");
    }

    private async Task<ModelPricing?> FindPricingByProviderCodeAsync(
        string providerType,
        string providerProviderId,
        CancellationToken cancellationToken)
    {
        if (providerType == ModelInvocationProviderTypes.Llm)
        {
            return await _dbContext.ModelPrices
                .Join(
                    _dbContext.LLMProviders,
                    pricing => pricing.ProviderId,
                    provider => provider.Id,
                    (pricing, provider) => new { pricing, provider })
                .Where(item =>
                    item.pricing.ProviderType == providerType &&
                    item.provider.ProviderId == providerProviderId &&
                    item.pricing.IsEnabled)
                .Select(item => item.pricing)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (providerType == ModelInvocationProviderTypes.Embedding)
        {
            return await _dbContext.ModelPrices
                .Join(
                    _dbContext.EmbeddingProviders,
                    pricing => pricing.ProviderId,
                    provider => provider.Id,
                    (pricing, provider) => new { pricing, provider })
                .Where(item =>
                    item.pricing.ProviderType == providerType &&
                    item.provider.ProviderId == providerProviderId &&
                    item.pricing.IsEnabled)
                .Select(item => item.pricing)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return null;
    }

    private async Task<string?> GetProviderCodeAsync(
        string providerType,
        Guid providerId,
        CancellationToken cancellationToken)
    {
        if (providerType == ModelInvocationProviderTypes.Llm)
        {
            return await _dbContext.LLMProviders
                .Where(item => item.Id == providerId)
                .Select(item => item.ProviderId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (providerType == ModelInvocationProviderTypes.Embedding)
        {
            return await _dbContext.EmbeddingProviders
                .Where(item => item.Id == providerId)
                .Select(item => item.ProviderId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return null;
    }
}
