using DevNexus.Shared.Enums;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 存储供应商创建请求DTO
/// </summary>
public class CreateStorageProviderRequest
{
    /// <summary>
    /// 供应商唯一标识 (如: aws-s3, aliyun-oss, qiniu-kodo, minio-local)
    /// </summary>
    public string ProviderId { get; set; } = string.Empty;
    
    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// 供应商类型
    /// </summary>
    public StorageProviderType Type { get; set; }
    
    /// <summary>
    /// 供应商Logo URL
    /// </summary>
    public string? LogoUrl { get; set; }
    
    /// <summary>
    /// S3 Endpoint
    /// </summary>
    public string ServiceUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// Access Key ID
    /// </summary>
    public string AccessKeyId { get; set; } = string.Empty;
    
    /// <summary>
    /// Secret Access Key
    /// </summary>
    public string SecretAccessKey { get; set; } = string.Empty;
    
    /// <summary>
    /// 存储桶名称
    /// </summary>
    public string BucketName { get; set; } = string.Empty;
    
    /// <summary>
    /// Region
    /// </summary>
    public string Region { get; set; } = "us-east-1";
    
    /// <summary>
    /// 是否使用路径样式访问
    /// </summary>
    public bool ForcePathStyle { get; set; } = false;
    
    /// <summary>
    /// CDN 域名（可选）
    /// </summary>
    public string? CdnDomain { get; set; }
    
    /// <summary>
    /// 是否使用 HTTPS
    /// </summary>
    public bool UseHttps { get; set; } = true;
    
    /// <summary>
    /// 预签名 URL 过期时间（秒）
    /// </summary>
    public int PresignedUrlExpirationSeconds { get; set; } = 3600;
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// 是否为默认供应商
    /// </summary>
    public bool IsDefault { get; set; } = false;
    
    /// <summary>
    /// 优先级 (数字越小优先级越高)
    /// </summary>
    public int Priority { get; set; } = 100;
    
    /// <summary>
    /// 配置元数据
    /// </summary>
    public Dictionary<string, object>? Configuration { get; set; }
}

/// <summary>
/// 存储供应商更新请求DTO
/// </summary>
public class UpdateStorageProviderRequest
{
    /// <summary>
    /// 显示名称
    /// </summary>
    public string? DisplayName { get; set; }
    
    /// <summary>
    /// 供应商Logo URL
    /// </summary>
    public string? LogoUrl { get; set; }
    
    /// <summary>
    /// S3 Endpoint
    /// </summary>
    public string? ServiceUrl { get; set; }
    
    /// <summary>
    /// Access Key ID
    /// </summary>
    public string? AccessKeyId { get; set; }
    
    /// <summary>
    /// Secret Access Key
    /// </summary>
    public string? SecretAccessKey { get; set; }
    
    /// <summary>
    /// 存储桶名称
    /// </summary>
    public string? BucketName { get; set; }
    
    /// <summary>
    /// Region
    /// </summary>
    public string? Region { get; set; }
    
    /// <summary>
    /// 是否使用路径样式访问
    /// </summary>
    public bool? ForcePathStyle { get; set; }
    
    /// <summary>
    /// CDN 域名
    /// </summary>
    public string? CdnDomain { get; set; }
    
    /// <summary>
    /// 是否使用 HTTPS
    /// </summary>
    public bool? UseHttps { get; set; }
    
    /// <summary>
    /// 预签名 URL 过期时间（秒）
    /// </summary>
    public int? PresignedUrlExpirationSeconds { get; set; }
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool? IsEnabled { get; set; }
    
    /// <summary>
    /// 是否为默认供应商
    /// </summary>
    public bool? IsDefault { get; set; }
    
    /// <summary>
    /// 优先级
    /// </summary>
    public int? Priority { get; set; }
    
    /// <summary>
    /// 配置元数据
    /// </summary>
    public Dictionary<string, object>? Configuration { get; set; }
}

/// <summary>
/// 存储供应商响应DTO
/// </summary>
public class StorageProviderResponse
{
    /// <summary>
    /// 供应商ID
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// 供应商唯一标识
    /// </summary>
    public string ProviderId { get; set; } = string.Empty;
    
    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// 供应商类型
    /// </summary>
    public StorageProviderType Type { get; set; }
    
    /// <summary>
    /// 供应商Logo URL
    /// </summary>
    public string? LogoUrl { get; set; }
    
    /// <summary>
    /// S3 Endpoint
    /// </summary>
    public string ServiceUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// 存储桶名称
    /// </summary>
    public string BucketName { get; set; } = string.Empty;
    
    /// <summary>
    /// Region
    /// </summary>
    public string Region { get; set; } = string.Empty;
    
    /// <summary>
    /// 是否使用路径样式访问
    /// </summary>
    public bool ForcePathStyle { get; set; }
    
    /// <summary>
    /// CDN 域名
    /// </summary>
    public string? CdnDomain { get; set; }
    
    /// <summary>
    /// 是否使用 HTTPS
    /// </summary>
    public bool UseHttps { get; set; }
    
    /// <summary>
    /// 预签名 URL 过期时间（秒）
    /// </summary>
    public int PresignedUrlExpirationSeconds { get; set; }
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; }
    
    /// <summary>
    /// 是否为默认供应商
    /// </summary>
    public bool IsDefault { get; set; }
    
    /// <summary>
    /// 优先级
    /// </summary>
    public int Priority { get; set; }
    
    /// <summary>
    /// 配置元数据
    /// </summary>
    public Dictionary<string, object> Configuration { get; set; } = new();
    
    /// <summary>
    /// 验证状态
    /// </summary>
    public ValidationStatus ValidationStatus { get; set; }
    
    /// <summary>
    /// 验证错误消息
    /// </summary>
    public string? ValidationError { get; set; }
    
    /// <summary>
    /// 最后验证时间
    /// </summary>
    public DateTime? LastValidatedAt { get; set; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
