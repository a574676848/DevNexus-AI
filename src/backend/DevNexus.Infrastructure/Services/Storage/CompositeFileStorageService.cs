// using DevNexus.Domain.Abstractions via GlobalUsings
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Storage;

/// <summary>
/// 组合文件存储服务
/// 智能选择存储后端：优先使用数据库配置的 S3 供应商，无配置时兜底使用本地存储
/// 通过 IStorageProviderManagementService 读取缓存，不直接查询数据库
/// </summary>
public class CompositeFileStorageService : IFileStorageService
{
    private readonly IStorageProviderManagementService _providerService;
    private readonly S3FileStorageService _s3Storage;
    private readonly LocalFileStorageService _localStorage;
    private readonly ILogger<CompositeFileStorageService> _logger;
    
    /// <inheritdoc />
    public string Provider => GetCurrentProviderAsync().GetAwaiter().GetResult();
    
    public CompositeFileStorageService(
        IStorageProviderManagementService providerService,
        S3FileStorageService s3Storage,
        LocalFileStorageService localStorage,
        ILogger<CompositeFileStorageService> logger)
    {
        _providerService = providerService;
        _s3Storage = s3Storage;
        _localStorage = localStorage;
        _logger = logger;
    }
    
    /// <summary>
    /// 获取当前应使用的存储服务
    /// 通过 IStorageProviderManagementService 获取供应商列表（已缓存）
    /// </summary>
    private async Task<IFileStorageService> GetStorageServiceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 通过管理服务获取供应商列表（已走 Redis 缓存）
            var providers = await _providerService.GetAllProvidersAsync(
                includeDisabled: false, 
                cancellationToken);
            
            var hasProvider = providers.Any();
            
            if (hasProvider)
            {
                _logger.LogDebug("[Storage.Composite] Using S3 storage (configured provider found)");
                return _s3Storage;
            }
            else
            {
                _logger.LogDebug("[Storage.Composite] No storage provider configured, falling back to local storage");
                return _localStorage;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Storage.Composite] Failed to check storage providers, falling back to local storage");
            return _localStorage;
        }
    }
    
    /// <summary>
    /// 获取当前提供商名称
    /// </summary>
    private async Task<string> GetCurrentProviderAsync()
    {
        var service = await GetStorageServiceAsync();
        return service.Provider;
    }
    
    /// <inheritdoc />
    public async Task<PresignedUploadInfo> GeneratePresignedUploadAsync(
        string fileName,
        string contentType,
        string? folder = null,
        CancellationToken cancellationToken = default)
    {
        var service = await GetStorageServiceAsync(cancellationToken);
        return await service.GeneratePresignedUploadAsync(fileName, contentType, folder, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<string> UploadFileAsync(
        Stream stream,
        string objectKey,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var service = await GetStorageServiceAsync(cancellationToken);
        return await service.UploadFileAsync(stream, objectKey, contentType, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<Stream> DownloadFileAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        var service = await GetStorageServiceAsync(cancellationToken);
        return await service.DownloadFileAsync(fileUrl, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<bool> DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        var service = await GetStorageServiceAsync(cancellationToken);
        return await service.DeleteFileAsync(fileUrl, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<bool> FileExistsAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        var service = await GetStorageServiceAsync(cancellationToken);
        return await service.FileExistsAsync(fileUrl, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<long> GetFileSizeAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        var service = await GetStorageServiceAsync(cancellationToken);
        return await service.GetFileSizeAsync(fileUrl, cancellationToken);
    }
    
    /// <inheritdoc />
    public async Task<string> GeneratePresignedUrlAsync(
        string fileUrl,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        var service = await GetStorageServiceAsync(cancellationToken);
        return await service.GeneratePresignedUrlAsync(fileUrl, expiresIn, cancellationToken);
    }
}
