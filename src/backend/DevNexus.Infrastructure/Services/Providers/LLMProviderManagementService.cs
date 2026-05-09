using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
// using DevNexus.Domain.Abstractions via GlobalUsings
// using DevNexus.Domain.Entities via GlobalUsings
using DevNexus.Infrastructure.Models;
using DevNexus.Infrastructure.Services.LLM;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Infrastructure.Services.Providers;

/// <summary>
/// LLM供应商管理服务实现
/// </summary>
public class LLMProviderManagementService : ILLMProviderManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly LLMProviderCache _providerCache;
    private readonly LLMProviderCacheState _providerCacheState;
    private readonly ILogger<LLMProviderManagementService> _logger;
    
    public LLMProviderManagementService(
        ApplicationDbContext context,
        IEncryptionService encryptionService,
        LLMProviderCache providerCache,
        LLMProviderCacheState providerCacheState,
        ILogger<LLMProviderManagementService> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _providerCache = providerCache;
        _providerCacheState = providerCacheState;
        _logger = logger;
    }
    
    public async Task<IEnumerable<LLMProviderResponse>> GetAllProvidersAsync(
        bool includeDisabled = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.LLMProviders.AsQueryable();
        
        if (!includeDisabled)
        {
            query = query.Where(p => p.IsEnabled);
        }
        
        var providers = await query
            .OrderBy(p => p.Priority)
            .ThenBy(p => p.DisplayName)
            .ToListAsync(cancellationToken);
        
        return providers.Select(MapToResponse);
    }
    
    public async Task<LLMProviderResponse?> GetProviderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.LLMProviders.FindAsync(
            new object[] { id },
            cancellationToken);
            
        return provider == null ? null : MapToResponse(provider);
    }
    
    public async Task<LLMProviderResponse?> GetProviderByProviderIdAsync(
        string providerId,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.LLMProviders
            .FirstOrDefaultAsync(p => p.ProviderId == providerId, cancellationToken);
            
        return provider == null ? null : MapToResponse(provider);
    }
    
    public async Task<LLMProviderResponse?> GetDefaultProviderAsync(
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.LLMProviders
            .Where(p => p.IsEnabled && p.IsDefault)
            .OrderBy(p => p.Priority)
            .FirstOrDefaultAsync(cancellationToken);
            
        return provider == null ? null : MapToResponse(provider);
    }
    
    public async Task<LLMProviderResponse> CreateProviderAsync(
        CreateLLMProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        
        var provider = new LLMProvider
        {
            ProviderId = request.ProviderId,
            DisplayName = request.DisplayName,
            Type = request.Type,
            LogoUrl = request.LogoUrl,
            Endpoint = request.Endpoint,
            ApiKey = _encryptionService.Encrypt(request.ApiKey), // 加密存储
            ModelName = request.ModelName,
            IsEnabled = request.IsEnabled,
            IsDefault = request.IsDefault,
            Priority = request.Priority,
            Configuration = request.Configuration ?? new()
        };
        
        // 如果设置为默认,取消其他默认
        if (provider.IsDefault)
        {
            await UnsetAllDefaultsAsync(cancellationToken);
        }
        
        _context.LLMProviders.Add(provider);
        await _context.SaveChangesAsync(cancellationToken);
        
        _logger.LogDebug(
            "Created LLM provider: {ProviderId} (ID: {Id})",
            provider.ProviderId,
            provider.Id);
        
        return MapToResponse(provider);
    }
    
    public async Task<LLMProviderResponse> UpdateProviderAsync(
        Guid id,
        UpdateLLMProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.LLMProviders.FindAsync(
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
        if (request.ModelName != null)
            provider.ModelName = request.ModelName;
        if (request.IsEnabled.HasValue)
            provider.IsEnabled = request.IsEnabled.Value;
        if (request.Priority.HasValue)
            provider.Priority = request.Priority.Value;
        if (request.Configuration != null)
            provider.Configuration = request.Configuration;
        
        // 处理默认设置
        if (request.IsDefault.HasValue && request.IsDefault.Value && !provider.IsDefault)
        {
            await UnsetAllDefaultsAsync(cancellationToken);
            provider.IsDefault = true;
        }
        else if (request.IsDefault.HasValue)
        {
            provider.IsDefault = request.IsDefault.Value;
        }
        
        provider.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _providerCache.Remove(provider.Id);
        _providerCacheState.Increment();
        
        _logger.LogDebug(
            "Updated LLM provider: {ProviderId} (ID: {Id})",
            provider.ProviderId,
            provider.Id);
        
        return MapToResponse(provider);
    }
    
    public async Task<bool> DeleteProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.LLMProviders.FindAsync(
            new object[] { id },
            cancellationToken);
            
        if (provider == null)
        {
            return false;
        }
        
        // 软删除
        provider.IsDeleted = true;
        provider.DeletedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync(cancellationToken);

        _providerCache.Remove(provider.Id);
        _providerCacheState.Increment();
        
        _logger.LogDebug(
            "Deleted LLM provider: {ProviderId} (ID: {Id})",
            provider.ProviderId,
            provider.Id);
        
        return true;
    }
    
    public async Task<bool> SetDefaultProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.LLMProviders.FindAsync(
            new object[] { id },
            cancellationToken);
            
        if (provider == null)
        {
            return false;
        }
        
        await UnsetAllDefaultsAsync(cancellationToken);
        
        provider.IsDefault = true;
        provider.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync(cancellationToken);

        _providerCache.Remove(provider.Id);
        _providerCacheState.Increment();
        
        _logger.LogDebug(
            "Set default LLM provider: {ProviderId} (ID: {Id})",
            provider.ProviderId,
            provider.Id);
        
        return true;
    }
    
    public async Task<ValidateProviderResponse> ValidateProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.LLMProviders.FindAsync(
            new object[] { id },
            cancellationToken);
            
        if (provider == null)
        {
            throw new KeyNotFoundException($"Provider not found: {id}");
        }
        
        try
        {
            // 简单的配置验证
            if (string.IsNullOrWhiteSpace(provider.Endpoint))
            {
                throw new InvalidOperationException("必须填写 API 端点地址");
            }
            
            if (string.IsNullOrWhiteSpace(provider.ApiKey))
            {
                throw new InvalidOperationException("必须填写 API 密钥");
            }
            
            if (string.IsNullOrWhiteSpace(provider.ModelName))
            {
                throw new InvalidOperationException("必须填写模型名称");
            }
            
            // 更新验证状态
            provider.ValidationStatus = ValidationStatus.Valid;
            provider.LastValidatedAt = DateTime.UtcNow;
            provider.ValidationError = null;
            
            await _context.SaveChangesAsync(cancellationToken);
            
            return new ValidateProviderResponse
            {
                IsValid = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Validation failed for provider {ProviderId}",
                provider.ProviderId);
            
            provider.ValidationStatus = ValidationStatus.Invalid;
            provider.LastValidatedAt = DateTime.UtcNow;
            provider.ValidationError = ex.Message;
            
            await _context.SaveChangesAsync(cancellationToken);
            
            return new ValidateProviderResponse
            {
                IsValid = false,
                ErrorMessage = ex.Message
            };
        }
    }
    
    public async Task<ValidateProviderResponse> TestProviderConnectionAsync(
        CreateLLMProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 简单的配置验证
            if (string.IsNullOrWhiteSpace(request.Endpoint))
            {
                throw new InvalidOperationException("必须填写 API 端点地址");
            }
            
            if (string.IsNullOrWhiteSpace(request.ApiKey))
            {
                throw new InvalidOperationException("必须填写 API 密钥");
            }
            
            if (string.IsNullOrWhiteSpace(request.ModelName))
            {
                throw new InvalidOperationException("必须填写模型名称");
            }
            
            return new ValidateProviderResponse
            {
                IsValid = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Test connection failed");
            
            return new ValidateProviderResponse
            {
                IsValid = false,
                ErrorMessage = ex.Message
            };
        }
    }
    
    private LLMProviderResponse MapToResponse(LLMProvider provider)
    {
        return new LLMProviderResponse
        {
            Id = provider.Id,
            ProviderId = provider.ProviderId,
            DisplayName = provider.DisplayName,
            Type = provider.Type,
            LogoUrl = provider.LogoUrl,
            Endpoint = provider.Endpoint,
            ModelName = provider.ModelName,
            IsEnabled = provider.IsEnabled,
            IsDefault = provider.IsDefault,
            Priority = provider.Priority,
            Configuration = provider.Configuration,
            ValidationStatus = provider.ValidationStatus,
            ValidationError = provider.ValidationError,
            LastValidatedAt = provider.LastValidatedAt,
            CreatedAt = provider.CreatedAt,
            UpdatedAt = provider.UpdatedAt
            // 注意: ApiKey 不返回
        };
    }
    
    private async Task UnsetAllDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var defaultProviders = await _context.LLMProviders
            .Where(p => p.IsDefault)
            .ToListAsync(cancellationToken);
            
        foreach (var provider in defaultProviders)
        {
            provider.IsDefault = false;
            provider.UpdatedAt = DateTime.UtcNow;
        }
    }
    
    /// <summary>
    /// 获取解密的 API Key
    /// </summary>
    public async Task<string> GetDecryptedApiKeyAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.LLMProviders.FindAsync(
            new object[] { id },
            cancellationToken);
            
        if (provider == null)
        {
            throw new KeyNotFoundException($"Provider not found: {id}");
        }
        
        return _encryptionService.Decrypt(provider.ApiKey);
    }
}
