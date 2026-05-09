using DevNexus.Domain.Entities.Base;
using DevNexus.Shared.Enums;

namespace DevNexus.Domain.Entities;

/// <summary>
/// 存储供应商配置实体
/// 支持 S3 兼容存储 (AWS S3, 阿里云 OSS, 七牛云 Kodo, 腾讯云 COS, MinIO 等)
/// </summary>
public class StorageProvider : AuditableEntity
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
    /// S3 Endpoint（例如：s3.amazonaws.com, oss-cn-hangzhou.aliyuncs.com）
    /// </summary>
    public string ServiceUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// Access Key ID (加密存储)
    /// </summary>
    public string AccessKeyId { get; set; } = string.Empty;
    
    /// <summary>
    /// Secret Access Key (加密存储)
    /// </summary>
    public string SecretAccessKey { get; set; } = string.Empty;
    
    /// <summary>
    /// 存储桶名称
    /// </summary>
    public string BucketName { get; set; } = string.Empty;
    
    /// <summary>
    /// Region（AWS 必填，其他厂商可选）
    /// </summary>
    public string Region { get; set; } = "us-east-1";
    
    /// <summary>
    /// 是否使用路径样式访问（Path-Style Access）
    /// 某些 S3 兼容服务需要设置为 true
    /// </summary>
    public bool ForcePathStyle { get; set; } = false;
    
    /// <summary>
    /// CDN 域名（可选，用于加速访问）
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
    /// 配置元数据 (JSONB)
    /// </summary>
    public Dictionary<string, object> Configuration { get; set; } = new();
    
    /// <summary>
    /// 最后验证时间
    /// </summary>
    public DateTime? LastValidatedAt { get; set; }
    
    /// <summary>
    /// 验证状态
    /// </summary>
    public ValidationStatus ValidationStatus { get; set; } = ValidationStatus.Unknown;
    
    /// <summary>
    /// 验证错误消息
    /// </summary>
    public string? ValidationError { get; set; }
}
