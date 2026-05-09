using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using DevNexus.Infrastructure.Models;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using System.Text.Json;
using Amazon.S3;
using Amazon.Runtime;
using Amazon.S3.Model;

namespace DevNexus.Infrastructure.Services.Providers;

/// <summary>
/// 存储供应商管理服务实现
/// 支持 Redis 缓存：读取时优先缓存，CRUD 操作时清除缓存
/// </summary>
public class StorageProviderManagementService : IStorageProviderManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly IDistributedCache _cache;
    private readonly ILogger<StorageProviderManagementService> _logger;
    
    // 缓存键前缀
    private const string CacheKeyPrefix = "storage:providers:";
    private const string CacheKeyAll = CacheKeyPrefix + "all";
    private const string CacheKeyAllWithDisabled = CacheKeyPrefix + "all:disabled";
    private const string CacheKeyDefault = CacheKeyPrefix + "default";
    private const string CacheKeyById = CacheKeyPrefix + "id:";
    private const string CacheKeyByProviderId = CacheKeyPrefix + "pid:";
    
    // 缓存过期时间
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
        SlidingExpiration = TimeSpan.FromMinutes(10)
    };
    
    public StorageProviderManagementService(
        ApplicationDbContext context,
        IEncryptionService encryptionService,
        IDistributedCache cache,
        ILogger<StorageProviderManagementService> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _cache = cache;
        _logger = logger;
    }
    
    public async Task<IEnumerable<StorageProviderResponse>> GetAllProvidersAsync(
        bool includeDisabled = false,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = includeDisabled ? CacheKeyAllWithDisabled : CacheKeyAll;
        
        // 尝试从缓存读取
        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cached))
        {
            _logger.LogDebug("[Storage.Cache] Hit | Key={CacheKey}", cacheKey);
            return JsonSerializer.Deserialize<List<StorageProviderResponse>>(cached) ?? [];
        }
        
        // 缓存未命中，从数据库加载
        _logger.LogDebug("[Storage.Cache] Miss | Key={CacheKey}", cacheKey);
        
        var query = _context.StorageProviders.AsQueryable();
        
        if (!includeDisabled)
        {
            query = query.Where(p => p.IsEnabled);
        }
        
        var providers = await query
            .OrderBy(p => p.Priority)
            .ThenBy(p => p.DisplayName)
            .ToListAsync(cancellationToken);
        
        var result = providers.Select(MapToResponse).ToList();
        
        // 写入缓存
        await _cache.SetStringAsync(
            cacheKey, 
            JsonSerializer.Serialize(result), 
            CacheOptions, 
            cancellationToken);
        
        return result;
    }
    
    public async Task<StorageProviderResponse?> GetProviderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeyById + id;
        
        // 尝试从缓存读取
        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cached))
        {
            _logger.LogDebug("[Storage.Cache] Hit | Key={CacheKey}", cacheKey);
            return JsonSerializer.Deserialize<StorageProviderResponse>(cached);
        }
        
        // 缓存未命中，从数据库加载
        var provider = await _context.StorageProviders.FindAsync(
            new object[] { id },
            cancellationToken);
            
        if (provider == null) return null;
        
        var result = MapToResponse(provider);
        
        // 写入缓存
        await _cache.SetStringAsync(
            cacheKey, 
            JsonSerializer.Serialize(result), 
            CacheOptions, 
            cancellationToken);
        
        return result;
    }
    
    public async Task<StorageProviderResponse?> GetProviderByProviderIdAsync(
        string providerId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeyByProviderId + providerId;
        
        // 尝试从缓存读取
        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cached))
        {
            _logger.LogDebug("[Storage.Cache] Hit | Key={CacheKey}", cacheKey);
            return JsonSerializer.Deserialize<StorageProviderResponse>(cached);
        }
        
        // 缓存未命中，从数据库加载
        var provider = await _context.StorageProviders
            .FirstOrDefaultAsync(p => p.ProviderId == providerId, cancellationToken);
            
        if (provider == null) return null;
        
        var result = MapToResponse(provider);
        
        // 写入缓存
        await _cache.SetStringAsync(
            cacheKey, 
            JsonSerializer.Serialize(result), 
            CacheOptions, 
            cancellationToken);
        
        return result;
    }
    
    public async Task<StorageProviderResponse?> GetDefaultProviderAsync(
        CancellationToken cancellationToken = default)
    {
        // 尝试从缓存读取
        var cached = await _cache.GetStringAsync(CacheKeyDefault, cancellationToken);
        if (!string.IsNullOrEmpty(cached))
        {
            _logger.LogDebug("[Storage.Cache] Hit | Key={Key}", CacheKeyDefault);
            return JsonSerializer.Deserialize<StorageProviderResponse>(cached);
        }
        
        // 缓存未命中，从数据库加载
        var provider = await _context.StorageProviders
            .Where(p => p.IsEnabled && p.IsDefault)
            .OrderBy(p => p.Priority)
            .FirstOrDefaultAsync(cancellationToken);
            
        if (provider == null) return null;
        
        var result = MapToResponse(provider);
        
        // 写入缓存
        await _cache.SetStringAsync(
            CacheKeyDefault, 
            JsonSerializer.Serialize(result), 
            CacheOptions, 
            cancellationToken);
        
        return result;
    }
    
    public async Task<StorageProviderResponse> CreateProviderAsync(
        CreateStorageProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        
        var provider = new StorageProvider
        {
            ProviderId = request.ProviderId,
            DisplayName = request.DisplayName,
            Type = request.Type,
            LogoUrl = request.LogoUrl,
            ServiceUrl = request.ServiceUrl,
            AccessKeyId = _encryptionService.Encrypt(request.AccessKeyId),
            SecretAccessKey = _encryptionService.Encrypt(request.SecretAccessKey),
            BucketName = request.BucketName,
            Region = request.Region,
            ForcePathStyle = request.ForcePathStyle,
            CdnDomain = request.CdnDomain,
            UseHttps = request.UseHttps,
            PresignedUrlExpirationSeconds = request.PresignedUrlExpirationSeconds,
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
        
        _context.StorageProviders.Add(provider);
        await _context.SaveChangesAsync(cancellationToken);
        
        // 清除所有缓存
        await InvalidateAllCacheAsync(cancellationToken);
        
        _logger.LogDebug(
            "Created Storage provider: {ProviderId} (ID: {Id})",
            provider.ProviderId,
            provider.Id);
        
        return MapToResponse(provider);
    }
    
    public async Task<StorageProviderResponse> UpdateProviderAsync(
        Guid id,
        UpdateStorageProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.StorageProviders.FindAsync(
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
        if (request.ServiceUrl != null)
            provider.ServiceUrl = request.ServiceUrl;
        if (request.AccessKeyId != null)
            provider.AccessKeyId = _encryptionService.Encrypt(request.AccessKeyId);
        if (request.SecretAccessKey != null)
            provider.SecretAccessKey = _encryptionService.Encrypt(request.SecretAccessKey);
        if (request.BucketName != null)
            provider.BucketName = request.BucketName;
        if (request.Region != null)
            provider.Region = request.Region;
        if (request.ForcePathStyle.HasValue)
            provider.ForcePathStyle = request.ForcePathStyle.Value;
        if (request.CdnDomain != null)
            provider.CdnDomain = request.CdnDomain;
        if (request.UseHttps.HasValue)
            provider.UseHttps = request.UseHttps.Value;
        if (request.PresignedUrlExpirationSeconds.HasValue)
            provider.PresignedUrlExpirationSeconds = request.PresignedUrlExpirationSeconds.Value;
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
        
        // 清除所有缓存
        await InvalidateAllCacheAsync(cancellationToken);
        
        _logger.LogDebug(
            "Updated Storage provider: {ProviderId} (ID: {Id})",
            provider.ProviderId,
            provider.Id);
        
        return MapToResponse(provider);
    }
    
    public async Task<bool> DeleteProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.StorageProviders.FindAsync(
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
        
        // 清除所有缓存
        await InvalidateAllCacheAsync(cancellationToken);
        
        _logger.LogDebug(
            "Deleted Storage provider: {ProviderId} (ID: {Id})",
            provider.ProviderId,
            provider.Id);
        
        return true;
    }
    
    public async Task<bool> SetDefaultProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.StorageProviders.FindAsync(
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
        
        // 清除所有缓存
        await InvalidateAllCacheAsync(cancellationToken);
        
        _logger.LogDebug(
            "Set default Storage provider: {ProviderId} (ID: {Id})",
            provider.ProviderId,
            provider.Id);
        
        return true;
    }
    
    /// <summary>
    /// 清除所有存储供应商缓存
    /// </summary>
    public async Task InvalidateAllCacheAsync(CancellationToken cancellationToken = default)
    {
        // 移除所有已知的缓存键
        // 注意：IDistributedCache 不支持按前缀删除，需要逐个删除已知的键
        var keysToRemove = new[]
        {
            CacheKeyAll,
            CacheKeyAllWithDisabled,
            CacheKeyDefault
        };
        
        foreach (var key in keysToRemove)
        {
            try
            {
                await _cache.RemoveAsync(key, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Storage.Cache] Failed to remove cache key: {Key}", key);
            }
        }
        
        _logger.LogDebug("[Storage.Cache] All caches invalidated");
    }
    
    public async Task<ValidateProviderResponse> ValidateProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.StorageProviders.FindAsync(
            new object[] { id },
            cancellationToken);
            
        if (provider == null)
        {
            throw new KeyNotFoundException($"Provider not found: {id}");
        }
        
        try
        {
            // 解密凭据
            var accessKeyId = _encryptionService.Decrypt(provider.AccessKeyId);
            var secretAccessKey = _encryptionService.Decrypt(provider.SecretAccessKey);
            
            // 测试 S3 连接
            var testResult = await TestS3ConnectionAsync(
                provider.ServiceUrl,
                accessKeyId,
                secretAccessKey,
                provider.BucketName,
                provider.Region,
                provider.ForcePathStyle,
                provider.UseHttps,
                cancellationToken);
            
            // 更新验证状态
            provider.ValidationStatus = testResult.IsValid ? ValidationStatus.Valid : ValidationStatus.Invalid;
            provider.LastValidatedAt = DateTime.UtcNow;
            provider.ValidationError = testResult.ErrorMessage;
            
            await _context.SaveChangesAsync(cancellationToken);
            
            // 清除该供应商的缓存
            await InvalidateAllCacheAsync(cancellationToken);
            
            return testResult;
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
        CreateStorageProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await TestS3ConnectionAsync(
                request.ServiceUrl,
                request.AccessKeyId,
                request.SecretAccessKey,
                request.BucketName,
                request.Region,
                request.ForcePathStyle,
                request.UseHttps,
                cancellationToken);
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
    
    /// <summary>
    /// 测试 S3 连接
    /// </summary>
    private async Task<ValidateProviderResponse> TestS3ConnectionAsync(
        string serviceUrl,
        string accessKeyId,
        string secretAccessKey,
        string bucketName,
        string region,
        bool forcePathStyle,
        bool useHttps,
        CancellationToken cancellationToken)
    {
        // 基础配置验证
        if (string.IsNullOrWhiteSpace(serviceUrl))
        {
            return new ValidateProviderResponse
            {
                IsValid = false,
                ErrorMessage = "Service URL is required"
            };
        }
        
        if (string.IsNullOrWhiteSpace(accessKeyId))
        {
            return new ValidateProviderResponse
            {
                IsValid = false,
                ErrorMessage = "Access Key ID is required"
            };
        }
        
        if (string.IsNullOrWhiteSpace(secretAccessKey))
        {
            return new ValidateProviderResponse
            {
                IsValid = false,
                ErrorMessage = "Secret Access Key is required"
            };
        }
        
        if (string.IsNullOrWhiteSpace(bucketName))
        {
            return new ValidateProviderResponse
            {
                IsValid = false,
                ErrorMessage = "Bucket name is required"
            };
        }
        
        try
        {
            // 配置 S3 客户端
            var config = new AmazonS3Config
            {
                ServiceURL = serviceUrl,
                ForcePathStyle = forcePathStyle,
                UseHttp = !useHttps
            };
            
            if (!string.IsNullOrEmpty(region))
            {
                config.AuthenticationRegion = region;
            }
            
            var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);
            using var s3Client = new AmazonS3Client(credentials, config);
            
            // 测试：列出 bucket 对象（只获取1个）
            var listRequest = new ListObjectsV2Request
            {
                BucketName = bucketName,
                MaxKeys = 1
            };
            
            await s3Client.ListObjectsV2Async(listRequest, cancellationToken);
            
            _logger.LogInformation(
                "[Storage.Validate] S3 connection test passed | Bucket={Bucket}",
                bucketName);
            
            return new ValidateProviderResponse
            {
                IsValid = true,
                Details = new Dictionary<string, object>
                {
                    ["bucket"] = bucketName,
                    ["region"] = region,
                    ["serviceUrl"] = serviceUrl
                }
            };
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogWarning(
                "[Storage.Validate] S3 connection test failed | ErrorCode={ErrorCode} Message={Message}",
                ex.ErrorCode,
                ex.Message);
            
            return new ValidateProviderResponse
            {
                IsValid = false,
                ErrorMessage = $"S3 Error ({ex.ErrorCode}): {ex.Message}"
            };
        }
    }
    
    private async Task UnsetAllDefaultsAsync(CancellationToken cancellationToken)
    {
        var defaultProviders = await _context.StorageProviders
            .Where(p => p.IsDefault)
            .ToListAsync(cancellationToken);
        
        foreach (var provider in defaultProviders)
        {
            provider.IsDefault = false;
            provider.UpdatedAt = DateTime.UtcNow;
        }
    }
    
    private StorageProviderResponse MapToResponse(StorageProvider provider)
    {
        return new StorageProviderResponse
        {
            Id = provider.Id,
            ProviderId = provider.ProviderId,
            DisplayName = provider.DisplayName,
            Type = provider.Type,
            LogoUrl = provider.LogoUrl,
            ServiceUrl = provider.ServiceUrl,
            BucketName = provider.BucketName,
            Region = provider.Region,
            ForcePathStyle = provider.ForcePathStyle,
            CdnDomain = provider.CdnDomain,
            UseHttps = provider.UseHttps,
            PresignedUrlExpirationSeconds = provider.PresignedUrlExpirationSeconds,
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
}
