namespace DevNexus.Domain.Configuration;

/// <summary>
/// 存储配置选项
/// 支持 S3 直传模式（生产环境）和本地存储（开发环境）
/// </summary>
public class StorageOptions
{
    /// <summary>
    /// 存储提供商类型: Local, S3
    /// 开发环境建议使用 Local，生产环境使用 S3
    /// </summary>
    public string Provider { get; set; } = "Local";
    
    /// <summary>
    /// 本地存储配置（开发环境使用）
    /// </summary>
    public LocalStorageOptions Local { get; set; } = new();
    
    /// <summary>
    /// S3 兼容存储配置（支持 AWS S3、阿里云 OSS、七牛云 Kodo 等）
    /// </summary>
    public S3StorageOptions S3 { get; set; } = new();
}

/// <summary>
/// 本地存储配置（开发环境）
/// </summary>
public class LocalStorageOptions
{
    /// <summary>
    /// 存储根目录
    /// </summary>
    public string RootPath { get; set; } = "wwwroot/uploads";
    
    /// <summary>
    /// 访问基础 URL（开发环境通常是 /uploads）
    /// </summary>
    public string BaseUrl { get; set; } = "/uploads";
}

/// <summary>
/// S3 兼容存储配置
/// 支持 AWS S3、阿里云 OSS、七牛云 Kodo、腾讯云 COS、MinIO 等
/// </summary>
public class S3StorageOptions
{
    /// <summary>
    /// Access Key ID
    /// </summary>
    public string AccessKeyId { get; set; } = string.Empty;
    
    /// <summary>
    /// Secret Access Key
    /// </summary>
    public string SecretAccessKey { get; set; } = string.Empty;
    
    /// <summary>
    /// S3 Endpoint（例如：s3.amazonaws.com, oss-cn-hangzhou.aliyuncs.com）
    /// </summary>
    public string ServiceUrl { get; set; } = string.Empty;
    
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
}
