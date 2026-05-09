namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 文件存储服务接口
/// 支持 S3 直传模式（生产环境）和本地存储（开发环境）
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// 获取存储提供商类型
    /// </summary>
    string Provider { get; }
    
    /// <summary>
    /// 生成预签名上传 URL（用于客户端直传）
    /// 本地存储模式下返回服务端上传端点
    /// </summary>
    /// <param name="fileName">文件名</param>
    /// <param name="contentType">内容类型</param>
    /// <param name="folder">文件夹（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>预签名上传信息</returns>
    Task<PresignedUploadInfo> GeneratePresignedUploadAsync(
        string fileName,
        string contentType,
        string? folder = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 上传文件（仅开发环境本地存储使用）
    /// S3 模式下客户端直接上传到 S3，不经过此方法
    /// </summary>
    /// <param name="stream">文件流</param>
    /// <param name="objectKey">对象键</param>
    /// <param name="contentType">内容类型</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文件访问 URL</returns>
    Task<string> UploadFileAsync(
        Stream stream,
        string objectKey,
        string contentType,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 下载文件
    /// </summary>
    /// <param name="fileUrl">文件URL</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文件流</returns>
    Task<Stream> DownloadFileAsync(string fileUrl, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 删除文件
    /// </summary>
    /// <param name="fileUrl">文件URL</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功删除</returns>
    Task<bool> DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 检查文件是否存在
    /// </summary>
    /// <param name="fileUrl">文件URL</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    Task<bool> FileExistsAsync(string fileUrl, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取文件大小
    /// </summary>
    /// <param name="fileUrl">文件URL</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文件大小（字节）</returns>
    Task<long> GetFileSizeAsync(string fileUrl, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 生成预签名URL（用于临时访问私有文件）
    /// </summary>
    /// <param name="fileUrl">文件URL</param>
    /// <param name="expiresIn">过期时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>预签名URL</returns>
    Task<string> GeneratePresignedUrlAsync(
        string fileUrl,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 预签名上传信息
/// </summary>
public record PresignedUploadInfo
{
    /// <summary>
    /// 上传 URL（S3 模式为预签名 URL，本地模式为服务端端点）
    /// </summary>
    public required string UploadUrl { get; init; }
    
    /// <summary>
    /// 文件访问 URL
    /// </summary>
    public required string FileUrl { get; init; }
    
    /// <summary>
    /// 对象键
    /// </summary>
    public required string ObjectKey { get; init; }
    
    /// <summary>
    /// 过期时间（UTC）
    /// </summary>
    public required DateTime ExpiresAt { get; init; }
    
    /// <summary>
    /// 上传方式: Direct（直接上传到存储）, Server（通过服务端中转）
    /// </summary>
    public required string UploadMethod { get; init; }
}
