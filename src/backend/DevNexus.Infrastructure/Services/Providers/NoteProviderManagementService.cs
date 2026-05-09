using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Infrastructure.Models;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Infrastructure.Services.Providers;

/// <summary>
/// 笔记供应商管理服务实现
/// </summary>
public class NoteProviderManagementService : INoteProviderManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<NoteProviderManagementService> _logger;

    public NoteProviderManagementService(
        ApplicationDbContext context,
        IEncryptionService encryptionService,
        ILogger<NoteProviderManagementService> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _logger = logger;
    }
    
    public async Task<IEnumerable<NoteProviderResponse>> GetAllProvidersAsync(
        bool includeDisabled = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.NoteProviders.AsQueryable();
        
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
    
    public async Task<NoteProviderResponse?> GetProviderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.NoteProviders.FindAsync(
            new object[] { id },
            cancellationToken);
            
        return provider == null ? null : MapToResponse(provider);
    }
    
    public async Task<NoteProviderResponse?> GetProviderByProviderIdAsync(
        string providerId,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.NoteProviders
            .FirstOrDefaultAsync(p => p.ProviderId == providerId, cancellationToken);
            
        return provider == null ? null : MapToResponse(provider);
    }
    
    public async Task<NoteProviderResponse?> GetDefaultProviderAsync(
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.NoteProviders
            .Where(p => p.IsEnabled && p.IsDefault)
            .OrderBy(p => p.Priority)
            .FirstOrDefaultAsync(cancellationToken);
            
        return provider == null ? null : MapToResponse(provider);
    }
    
    public async Task<NoteProviderResponse> CreateProviderAsync(
        CreateNoteProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = new NoteProvider
        {
            ProviderId = request.ProviderId,
            DisplayName = request.DisplayName,
            Type = request.Type,
            LogoUrl = request.LogoUrl,
            Endpoint = request.Endpoint,
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

        _context.NoteProviders.Add(provider);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Created Note provider: {ProviderId} (ID: {Id})",
            provider.ProviderId,
            provider.Id);

        return MapToResponse(provider);
    }
    
    public async Task<NoteProviderResponse> UpdateProviderAsync(
        Guid id,
        UpdateNoteProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.NoteProviders.FindAsync(
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
        
        _logger.LogDebug(
            "Updated Note provider: {ProviderId} (ID: {Id})",
            provider.ProviderId,
            provider.Id);
        
        return MapToResponse(provider);
    }
    
    public async Task<bool> DeleteProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.NoteProviders.FindAsync(
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
        
        _logger.LogDebug(
            "Deleted Note provider: {ProviderId} (ID: {Id})",
            provider.ProviderId,
            provider.Id);
        
        return true;
    }
    
    public async Task<bool> SetDefaultProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.NoteProviders.FindAsync(
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
        
        _logger.LogDebug(
            "Set default Note provider: {ProviderId} (ID: {Id})",
            provider.ProviderId,
            provider.Id);
        
        return true;
    }
    
    public async Task<ValidateNoteProviderResponse> ValidateProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.NoteProviders.FindAsync(
            new object[] { id },
            cancellationToken);

        if (provider == null)
        {
            throw new KeyNotFoundException($"Provider not found: {id}");
        }

        try
        {
            // 基础验证：检查配置是否完整
            var isValid = !string.IsNullOrWhiteSpace(provider.Endpoint);

            // 更新验证状态
            provider.ValidationStatus = isValid ? ValidationStatus.Valid : ValidationStatus.Invalid;
            provider.LastValidatedAt = DateTime.UtcNow;
            provider.ValidationError = isValid ? null : "Endpoint configuration is invalid";

            await _context.SaveChangesAsync(cancellationToken);

            return new ValidateNoteProviderResponse
            {
                IsValid = isValid,
                ValidatedAt = DateTime.UtcNow,
                Details = new Dictionary<string, object>
                {
                    { "endpoint", provider.Endpoint },
                    { "note", "Full connection validation requires user credentials. Users should test their integration after setup." }
                }
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

            return new ValidateNoteProviderResponse
            {
                IsValid = false,
                ErrorMessage = ex.Message,
                ValidatedAt = DateTime.UtcNow
            };
        }
    }
    
    public async Task<ValidateNoteProviderResponse> TestProviderConnectionAsync(
        CreateNoteProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // 占位，避免异步警告

        try
        {
            // 基础配置验证
            if (string.IsNullOrWhiteSpace(request.Endpoint))
            {
                throw new InvalidOperationException("必须填写 API 端点地址");
            }

            // 验证 Endpoint 格式
            if (!Uri.TryCreate(request.Endpoint, UriKind.Absolute, out var uri))
            {
                throw new InvalidOperationException("端点 URL 格式无效");
            }

            return new ValidateNoteProviderResponse
            {
                IsValid = true,
                ValidatedAt = DateTime.UtcNow,
                Details = new Dictionary<string, object>
                {
                    { "endpoint", request.Endpoint },
                    { "note", "Provider configuration is valid. Users need to add their credentials for full connection testing." }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Test connection failed");

            return new ValidateNoteProviderResponse
            {
                IsValid = false,
                ErrorMessage = ex.Message,
                ValidatedAt = DateTime.UtcNow
            };
        }
    }

    private NoteProviderResponse MapToResponse(NoteProvider provider)
    {
        return new NoteProviderResponse
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
            ValidationStatus = provider.ValidationStatus,
            ValidationError = provider.ValidationError,
            LastValidatedAt = provider.LastValidatedAt,
            CreatedAt = provider.CreatedAt,
            UpdatedAt = provider.UpdatedAt
            // 注意: AccessToken 不返回
        };
    }
    
    private async Task UnsetAllDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var defaultProviders = await _context.NoteProviders
            .Where(p => p.IsDefault)
            .ToListAsync(cancellationToken);
            
        foreach (var provider in defaultProviders)
        {
            provider.IsDefault = false;
            provider.UpdatedAt = DateTime.UtcNow;
        }
    }
}
