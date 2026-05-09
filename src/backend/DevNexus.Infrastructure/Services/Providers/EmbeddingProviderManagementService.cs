using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
// using DevNexus.Domain.Abstractions via GlobalUsings
using DevNexus.Domain.Configuration;
// using DevNexus.Domain.Entities via GlobalUsings
using DevNexus.Infrastructure.Models;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Infrastructure.Services.Providers;

/// <summary>
/// Embedding供应商管理服务实现
/// </summary>
public class EmbeddingProviderManagementService : IEmbeddingProviderManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<EmbeddingProviderManagementService> _logger;
    private readonly int _globalVectorSize;
    
    public EmbeddingProviderManagementService(
        ApplicationDbContext context,
        IEncryptionService encryptionService,
        IOptions<QdrantOptions> qdrantOptions,
        ILogger<EmbeddingProviderManagementService> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _globalVectorSize = (int)(qdrantOptions?.Value?.VectorSize ?? 1024);
        _logger = logger;
    }

    /// <inheritdoc />
    public int GetGlobalVectorSize() => _globalVectorSize;
    
    public async Task<IEnumerable<EmbeddingProviderResponse>> GetAllProvidersAsync(
        bool includeDisabled = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.EmbeddingProviders.AsQueryable();
        
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
    
    public async Task<EmbeddingProviderResponse?> GetProviderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.EmbeddingProviders.FindAsync(
            new object[] { id },
            cancellationToken);
            
        return provider == null ? null : MapToResponse(provider);
    }
    
    public async Task<EmbeddingProviderResponse?> GetProviderByProviderIdAsync(
        string providerId,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.EmbeddingProviders
            .FirstOrDefaultAsync(p => p.ProviderId == providerId, cancellationToken);
            
        return provider == null ? null : MapToResponse(provider);
    }
    
    public async Task<EmbeddingProviderResponse?> GetDefaultProviderAsync(
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.EmbeddingProviders
            .Where(p => p.IsEnabled && p.IsDefault)
            .OrderBy(p => p.Priority)
            .FirstOrDefaultAsync(cancellationToken);
            
        return provider == null ? null : MapToResponse(provider);
    }
    
    public async Task<EmbeddingProviderResponse> CreateProviderAsync(
        CreateEmbeddingProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureRequestIsValid(request.Endpoint, request.ApiKey, request.ModelName);
        
        var provider = new EmbeddingProvider
        {
            ProviderId = request.ProviderId,
            DisplayName = request.DisplayName,
            Type = request.Type,
            LogoUrl = request.LogoUrl,
            Endpoint = request.Endpoint,
            ApiKey = _encryptionService.Encrypt(request.ApiKey),
            ModelName = request.ModelName,
            VectorSize = _globalVectorSize,
            IsEnabled = request.IsEnabled,
            IsDefault = request.IsDefault,
            Priority = request.Priority,
            Configuration = request.Configuration ?? new()
        };
        
        if (provider.IsDefault)
        {
            await UnsetAllDefaultsAsync(cancellationToken);
        }
        
        _context.EmbeddingProviders.Add(provider);
        await _context.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation(
            "Created Embedding provider: {ProviderId} (ID: {Id})",
            provider.ProviderId,
            provider.Id);
        
        return MapToResponse(provider);
    }
    
    public async Task<EmbeddingProviderResponse> UpdateProviderAsync(
        Guid id,
        UpdateEmbeddingProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.EmbeddingProviders.FindAsync(
            new object[] { id },
            cancellationToken);
            
        if (provider == null)
        {
            throw new KeyNotFoundException($"Provider not found: {id}");
        }
        
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
        
        if (request.IsDefault.HasValue && request.IsDefault.Value && !provider.IsDefault)
        {
            await UnsetAllDefaultsAsync(cancellationToken);
            provider.IsDefault = true;
        }
        else if (request.IsDefault.HasValue)
        {
            provider.IsDefault = request.IsDefault.Value;
        }
        
        provider.VectorSize = _globalVectorSize;
        provider.UpdatedAt = DateTime.UtcNow;

        EnsureRequestIsValid(provider.Endpoint, _encryptionService.Decrypt(provider.ApiKey), provider.ModelName);
        await _context.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation(
            "Updated Embedding provider: {ProviderId} (ID: {Id})",
            provider.ProviderId,
            provider.Id);
        
        return MapToResponse(provider);
    }
    
    public async Task<bool> DeleteProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.EmbeddingProviders.FindAsync(
            new object[] { id },
            cancellationToken);
            
        if (provider == null)
        {
            return false;
        }
        
        provider.IsDeleted = true;
        provider.DeletedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation(
            "Deleted Embedding provider: {ProviderId} (ID: {Id})",
            provider.ProviderId,
            provider.Id);
        
        return true;
    }
    
    public async Task<bool> SetDefaultProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.EmbeddingProviders.FindAsync(
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
        
        _logger.LogInformation(
            "Set default Embedding provider: {ProviderId} (ID: {Id})",
            provider.ProviderId,
            provider.Id);
        
        return true;
    }
    
    public async Task<ValidateProviderResponse> ValidateProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.EmbeddingProviders.FindAsync(
            new object[] { id },
            cancellationToken);
            
        if (provider == null)
        {
            throw new KeyNotFoundException($"Provider not found: {id}");
        }
        
        try
        {
            EnsureRequestIsValid(provider.Endpoint, _encryptionService.Decrypt(provider.ApiKey), provider.ModelName);
            provider.VectorSize = _globalVectorSize;
            
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
        CreateEmbeddingProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureRequestIsValid(request.Endpoint, request.ApiKey, request.ModelName);
            
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
    
    private EmbeddingProviderResponse MapToResponse(EmbeddingProvider provider)
    {
        return new EmbeddingProviderResponse
        {
            Id = provider.Id,
            ProviderId = provider.ProviderId,
            DisplayName = provider.DisplayName,
            Type = provider.Type,
            LogoUrl = provider.LogoUrl,
            Endpoint = provider.Endpoint,
            ModelName = provider.ModelName,
            VectorSize = _globalVectorSize,
            IsEnabled = provider.IsEnabled,
            IsDefault = provider.IsDefault,
            Priority = provider.Priority,
            Configuration = provider.Configuration,
            ValidationStatus = provider.ValidationStatus,
            ValidationError = provider.ValidationError,
            LastValidatedAt = provider.LastValidatedAt,
            CreatedAt = provider.CreatedAt,
            UpdatedAt = provider.UpdatedAt
        };
    }
    
    private async Task UnsetAllDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var defaultProviders = await _context.EmbeddingProviders
            .Where(p => p.IsDefault)
            .ToListAsync(cancellationToken);
            
        foreach (var provider in defaultProviders)
        {
            provider.IsDefault = false;
            provider.UpdatedAt = DateTime.UtcNow;
        }
    }

    public async Task<ProviderCredentials?> GetProviderCredentialsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.EmbeddingProviders.FindAsync(
            new object[] { id },
            cancellationToken);
            
        if (provider == null || !provider.IsEnabled)
        {
            return null;
        }

        return new ProviderCredentials(
            Endpoint: provider.Endpoint ?? string.Empty,
            ApiKey: _encryptionService.Decrypt(provider.ApiKey),
            ModelName: provider.ModelName,
            VectorSize: _globalVectorSize,
            ProviderType: provider.Type.ToString()
        );
    }

    /// <summary>
    /// 统一校验 Embedding 供应商必填项。
    /// </summary>
    private static void EnsureRequestIsValid(string? endpoint, string? apiKey, string? modelName)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("必须填写 API 端点地址");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("必须填写 API 密钥");
        }

        if (string.IsNullOrWhiteSpace(modelName))
        {
            throw new InvalidOperationException("必须填写模型名称");
        }
    }
}
