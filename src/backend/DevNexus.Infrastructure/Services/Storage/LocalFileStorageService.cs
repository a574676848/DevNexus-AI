// using DevNexus.Domain.Abstractions via GlobalUsings
// using DevNexus.Domain.Configuration via GlobalUsings
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevNexus.Infrastructure.Services.Storage;

/// <summary>
/// 本地文件存储服务实现（开发环境使用）
/// 模拟 S3 直传的行为，但实际存储在本地文件系统
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly LocalStorageOptions _options;
    private readonly ILogger<LocalFileStorageService> _logger;
    private readonly string _uploadEndpoint;
    
    /// <inheritdoc />
    public string Provider => "Local";
    
    public LocalFileStorageService(
        IOptions<StorageOptions> options,
        ILogger<LocalFileStorageService> logger)
    {
        _options = options.Value.Local;
        _logger = logger;
        
        // 开发环境上传端点（通过服务端中转）
        _uploadEndpoint = "/api/v1/storage/upload";
        
        // 确保根目录存在
        if (!Directory.Exists(_options.RootPath))
        {
            Directory.CreateDirectory(_options.RootPath);
            _logger.LogDebug("[Storage.Local] Root directory created | Path={Path}", _options.RootPath);
        }
        
        _logger.LogDebug(
            "[Storage.Local] Initialized | RootPath={RootPath} BaseUrl={BaseUrl}",
            _options.RootPath,
            _options.BaseUrl);
    }
    
    /// <inheritdoc />
    public Task<PresignedUploadInfo> GeneratePresignedUploadAsync(
        string fileName,
        string contentType,
        string? folder = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var objectKey = BuildObjectKey(fileName, folder);
            var fileUrl = BuildUrl(objectKey);
            
            // 本地存储返回服务端上传端点，由服务端处理上传
            var uploadUrl = $"{_uploadEndpoint}?objectKey={Uri.EscapeDataString(objectKey)}&contentType={Uri.EscapeDataString(contentType)}";
            
            _logger.LogDebug(
                "[Storage.Local.PresignUpload] Upload info generated | ObjectKey={ObjectKey}",
                objectKey);
            
            return Task.FromResult(new PresignedUploadInfo
            {
                UploadUrl = uploadUrl,
                FileUrl = fileUrl,
                ObjectKey = objectKey,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                UploadMethod = "Server" // 通过服务端上传
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage.Local.PresignUpload] Failed | FileName={FileName}", fileName);
            throw;
        }
    }
    
    /// <inheritdoc />
    public async Task<string> UploadFileAsync(
        Stream stream,
        string objectKey,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = Path.Combine(_options.RootPath, objectKey.Replace('/', Path.DirectorySeparatorChar));
            var directory = Path.GetDirectoryName(fullPath);
            
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            await stream.CopyToAsync(fileStream, cancellationToken);
            
            var url = BuildUrl(objectKey);
            
            _logger.LogDebug(
                "[Storage.Local.Upload] File uploaded | ObjectKey={ObjectKey} Size={Size}",
                objectKey,
                stream.Length);
            
            return url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage.Local.Upload] Failed | ObjectKey={ObjectKey}", objectKey);
            throw;
        }
    }
    
    /// <inheritdoc />
    public async Task<Stream> DownloadFileAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var relativePath = ExtractRelativePath(fileUrl);
            var fullPath = Path.Combine(_options.RootPath, relativePath);
            
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"File not found: {fileUrl}");
            }
            
            var memoryStream = new MemoryStream();
            await using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
            await fileStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
            
            _logger.LogDebug("[Storage.Local.Download] File downloaded | Path={Path}", relativePath);
            
            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage.Local.Download] Failed | Url={Url}", fileUrl);
            throw;
        }
    }
    
    /// <inheritdoc />
    public Task<bool> DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var relativePath = ExtractRelativePath(fileUrl);
            var fullPath = Path.Combine(_options.RootPath, relativePath);
            
            if (!File.Exists(fullPath))
            {
                return Task.FromResult(false);
            }
            
            File.Delete(fullPath);
            
            _logger.LogDebug("[Storage.Local.Delete] File deleted | Path={Path}", relativePath);
            
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage.Local.Delete] Failed | Url={Url}", fileUrl);
            return Task.FromResult(false);
        }
    }
    
    /// <inheritdoc />
    public Task<bool> FileExistsAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var relativePath = ExtractRelativePath(fileUrl);
            var fullPath = Path.Combine(_options.RootPath, relativePath);
            return Task.FromResult(File.Exists(fullPath));
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
    
    /// <inheritdoc />
    public Task<long> GetFileSizeAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var relativePath = ExtractRelativePath(fileUrl);
            var fullPath = Path.Combine(_options.RootPath, relativePath);
            
            if (!File.Exists(fullPath))
            {
                return Task.FromResult(0L);
            }
            
            var fileInfo = new FileInfo(fullPath);
            return Task.FromResult(fileInfo.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage.Local.GetSize] Failed | Url={Url}", fileUrl);
            return Task.FromResult(0L);
        }
    }
    
    /// <inheritdoc />
    public Task<string> GeneratePresignedUrlAsync(
        string fileUrl,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        // 本地存储不需要预签名 URL，直接返回原 URL
        return Task.FromResult(fileUrl);
    }
    
    private string BuildObjectKey(string fileName, string? folder)
    {
        // 生成日期目录结构: YYYY/MM/DD
        var now = DateTime.UtcNow;
        var datePath = $"{now.Year:D4}/{now.Month:D2}/{now.Day:D2}";
        
        // 添加自定义文件夹
        if (!string.IsNullOrEmpty(folder))
        {
            datePath = $"{folder}/{datePath}";
        }
        
        // 生成唯一文件名
        var extension = Path.GetExtension(fileName);
        var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
        
        return $"{datePath}/{uniqueFileName}";
    }
    
    private string BuildUrl(string objectKey)
    {
        // 将路径分隔符统一为 URL 格式
        var urlPath = objectKey.Replace(Path.DirectorySeparatorChar, '/');
        return $"{_options.BaseUrl.TrimEnd('/')}/{urlPath}";
    }
    
    private string ExtractRelativePath(string fileUrl)
    {
        // 从 URL 中提取相对路径
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var relativePath = fileUrl.Replace(baseUrl + "/", "");
        return relativePath.Replace('/', Path.DirectorySeparatorChar);
    }
}
