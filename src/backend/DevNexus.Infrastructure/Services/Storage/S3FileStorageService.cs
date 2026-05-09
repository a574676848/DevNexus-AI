using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
// using DevNexus.Domain.Abstractions via GlobalUsings
using DevNexus.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Storage;

/// <summary>
/// S3 兼容存储服务实现
/// 支持 AWS S3、阿里云 OSS、七牛云 Kodo、腾讯云 COS、MinIO、Cloudflare R2 等所有 S3 兼容服务
/// 使用客户端直传模式，文件直接上传到 S3，不经过后端服务器
/// 配置从数据库读取，支持动态切换存储供应商
/// </summary>
public class S3FileStorageService : IFileStorageService
{
    private readonly ApplicationDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<S3FileStorageService> _logger;

    // 缓存当前的 S3 客户端和配置
    private IAmazonS3? _s3Client;
    private StorageProvider? _currentProvider;
    private readonly SemaphoreSlim _clientLock = new(1, 1);

    /// <inheritdoc />
    public string Provider => "S3";

    public S3FileStorageService(
        ApplicationDbContext context,
        IEncryptionService encryptionService,
        ILogger<S3FileStorageService> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    /// <summary>
    /// 获取或创建 S3 客户端
    /// </summary>
    private async Task<(IAmazonS3 Client, StorageProvider Provider)> GetS3ClientAsync(
        CancellationToken cancellationToken = default)
    {
        await _clientLock.WaitAsync(cancellationToken);
        try
        {
            // 获取默认的存储供应商
            var provider = await _context.StorageProviders
                .Where(p => p.IsEnabled && p.IsDefault)
                .OrderBy(p => p.Priority)
                .FirstOrDefaultAsync(cancellationToken);

            if (provider == null)
            {
                // 如果没有默认的，获取优先级最高的启用供应商
                provider = await _context.StorageProviders
                    .Where(p => p.IsEnabled)
                    .OrderBy(p => p.Priority)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (provider == null)
            {
                throw new InvalidOperationException(
                    "No storage provider configured. Please configure a storage provider in the admin panel.");
            }

            // 检查是否需要重新创建客户端
            if (_s3Client != null && _currentProvider != null &&
                _currentProvider.Id == provider.Id &&
                _currentProvider.UpdatedAt == provider.UpdatedAt)
            {
                return (_s3Client, _currentProvider);
            }

            // 解密凭据
            var accessKeyId = _encryptionService.Decrypt(provider.AccessKeyId);
            var secretAccessKey = _encryptionService.Decrypt(provider.SecretAccessKey);

            // 配置 S3 客户端
            var config = new AmazonS3Config
            {
                ServiceURL = provider.ServiceUrl,
                ForcePathStyle = provider.ForcePathStyle,
                UseHttp = !provider.UseHttps
            };

            // 如果指定了 Region，使用指定的 Region
            if (!string.IsNullOrEmpty(provider.Region))
            {
                config.AuthenticationRegion = provider.Region;
            }

            var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);

            // 释放旧客户端
            if (_s3Client is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _s3Client = new AmazonS3Client(credentials, config);
            _currentProvider = provider;

            _logger.LogDebug(
                "[Storage.S3] Client initialized | Provider={ProviderId} Endpoint={Endpoint} Bucket={Bucket}",
                provider.ProviderId,
                provider.ServiceUrl,
                provider.BucketName);

            return (_s3Client, provider);
        }
        finally
        {
            _clientLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<PresignedUploadInfo> GeneratePresignedUploadAsync(
        string fileName,
        string contentType,
        string? folder = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (client, provider) = await GetS3ClientAsync(cancellationToken);
            var objectKey = BuildObjectKey(fileName, folder);

            var request = new GetPreSignedUrlRequest
            {
                BucketName = provider.BucketName,
                Key = objectKey,
                Expires = DateTime.UtcNow.AddSeconds(provider.PresignedUrlExpirationSeconds),
                Verb = HttpVerb.PUT,
                ContentType = contentType
            };

            var uploadUrl = client.GetPreSignedURL(request);
            var fileUrl = BuildPublicUrl(objectKey, provider);

            _logger.LogDebug(
                "[Storage.S3.PresignUpload] Upload URL generated | Key={Key} ExpiresIn={Seconds}s Provider={Provider}",
                objectKey,
                provider.PresignedUrlExpirationSeconds,
                provider.ProviderId);

            return new PresignedUploadInfo
            {
                UploadUrl = uploadUrl,
                FileUrl = fileUrl,
                ObjectKey = objectKey,
                ExpiresAt = DateTime.UtcNow.AddSeconds(provider.PresignedUrlExpirationSeconds),
                UploadMethod = "Direct" // S3 直传
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage.S3.PresignUpload] Failed | FileName={FileName}", fileName);
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
            var (client, provider) = await GetS3ClientAsync(cancellationToken);

            var request = new PutObjectRequest
            {
                BucketName = provider.BucketName,
                Key = objectKey,
                InputStream = stream,
                ContentType = contentType,
                DisablePayloadSigning = true,  // 关键：禁用负载签名
                UseChunkEncoding = false        // 关键：禁用分块编码
            };

            await client.PutObjectAsync(request, cancellationToken);

            var fileUrl = BuildPublicUrl(objectKey, provider);

            _logger.LogDebug(
                "[Storage.S3.Upload] File uploaded via server | Key={Key} Provider={Provider}",
                objectKey,
                provider.ProviderId);

            return fileUrl;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(
                ex,
                "[Storage.S3.Upload] S3 upload error | StatusCode={StatusCode} ErrorCode={ErrorCode}",
                ex.StatusCode,
                ex.ErrorCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage.S3.Upload] Failed to upload file | Key={Key}", objectKey);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Stream> DownloadFileAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var (client, provider) = await GetS3ClientAsync(cancellationToken);
            var key = ExtractObjectKey(fileUrl, provider);

            var request = new GetObjectRequest
            {
                BucketName = provider.BucketName,
                Key = key
            };

            var response = await client.GetObjectAsync(request, cancellationToken);

            var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            _logger.LogDebug("[Storage.S3.Download] File downloaded | Key={Key}", key);

            return memoryStream;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(
                ex,
                "[Storage.S3.Download] S3 error | StatusCode={StatusCode} ErrorCode={ErrorCode}",
                ex.StatusCode,
                ex.ErrorCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage.S3.Download] Failed to download file | Url={Url}", fileUrl);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var (client, provider) = await GetS3ClientAsync(cancellationToken);
            var key = ExtractObjectKey(fileUrl, provider);

            var request = new DeleteObjectRequest
            {
                BucketName = provider.BucketName,
                Key = key
            };

            await client.DeleteObjectAsync(request, cancellationToken);

            _logger.LogDebug("[Storage.S3.Delete] File deleted | Key={Key}", key);

            return true;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(
                ex,
                "[Storage.S3.Delete] S3 error | StatusCode={StatusCode} ErrorCode={ErrorCode}",
                ex.StatusCode,
                ex.ErrorCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage.S3.Delete] Failed to delete file | Url={Url}", fileUrl);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> FileExistsAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var (client, provider) = await GetS3ClientAsync(cancellationToken);
            var key = ExtractObjectKey(fileUrl, provider);

            var request = new GetObjectMetadataRequest
            {
                BucketName = provider.BucketName,
                Key = key
            };

            await client.GetObjectMetadataAsync(request, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage.S3.Exists] Failed to check file existence | Url={Url}", fileUrl);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<long> GetFileSizeAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var (client, provider) = await GetS3ClientAsync(cancellationToken);
            var key = ExtractObjectKey(fileUrl, provider);

            var request = new GetObjectMetadataRequest
            {
                BucketName = provider.BucketName,
                Key = key
            };

            var response = await client.GetObjectMetadataAsync(request, cancellationToken);
            return response.ContentLength;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage.S3.GetSize] Failed to get file size | Url={Url}", fileUrl);
            return 0;
        }
    }

    /// <inheritdoc />
    public async Task<string> GeneratePresignedUrlAsync(
        string fileUrl,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (client, provider) = await GetS3ClientAsync(cancellationToken);
            var key = ExtractObjectKey(fileUrl, provider);

            var request = new GetPreSignedUrlRequest
            {
                BucketName = provider.BucketName,
                Key = key,
                Expires = DateTime.UtcNow.Add(expiresIn),
                Verb = HttpVerb.GET
            };

            var url = client.GetPreSignedURL(request);

            _logger.LogDebug(
                "[Storage.S3.Presign] URL generated | Key={Key} ExpiresIn={Expiry}",
                key,
                expiresIn);

            return url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage.S3.Presign] Failed to generate presigned URL | Url={Url}", fileUrl);
            throw;
        }
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

    private string BuildPublicUrl(string key, StorageProvider provider)
    {
        // 如果配置了 CDN 域名，使用 CDN 域名
        if (!string.IsNullOrEmpty(provider.CdnDomain))
        {
            var cdnDomain = provider.CdnDomain.Trim().TrimEnd('/');
            // 检查是否已经包含协议
            if (cdnDomain.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                cdnDomain.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return $"{cdnDomain}/{key}";
            }

            var protocol = provider.UseHttps ? "https" : "http";
            return $"{protocol}://{cdnDomain}/{key}";
        }

        // 否则使用默认的 S3 URL
        var serviceUrl = provider.ServiceUrl.Trim().TrimEnd('/');

        // 根据是否使用路径样式访问，构造不同的 URL
        if (provider.ForcePathStyle)
        {
            // 如果 ServiceUrl 没有协议头，补全协议
            if (!serviceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !serviceUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var protocol = provider.UseHttps ? "https" : "http";
                serviceUrl = $"{protocol}://{serviceUrl}";
            }
            return $"{serviceUrl}/{provider.BucketName}/{key}";
        }
        else
        {
            // 虚拟主机样式: bucket.endpoint/key
            // 移除协议头，确保大小写不敏感
            var endpoint = serviceUrl;
            if (endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                endpoint = endpoint.Substring(8);
            else if (endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                endpoint = endpoint.Substring(7);

            var protocol = provider.UseHttps ? "https" : "http";
            return $"{protocol}://{provider.BucketName}.{endpoint}/{key}";
        }
    }

    private string ExtractObjectKey(string fileUrl, StorageProvider provider)
    {
        // 从 URL 中提取对象键
        var uri = new Uri(fileUrl);
        // Uri.AbsolutePath 会对中文和特殊字符进行 URL 编码，需要解码以匹配 S3 中存储的原始 key
        var path = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');

        // 如果使用路径样式，需要移除 bucket 名称
        if (provider.ForcePathStyle && path.StartsWith(provider.BucketName + "/", StringComparison.OrdinalIgnoreCase))
        {
            path = path.Substring(provider.BucketName.Length + 1);
        }

        _logger.LogDebug("[Storage.S3.ExtractKey] Extracted key | FileUrl={FileUrl} Key={Key}", fileUrl, path);

        return path;
    }
}
