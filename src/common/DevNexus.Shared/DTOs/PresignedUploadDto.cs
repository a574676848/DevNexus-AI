using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 预签名上传 URL 请求
/// </summary>
public class PresignedUploadRequest
{
    /// <summary>
    /// 文件名
    /// </summary>
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// 内容类型
    /// </summary>
    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = "application/octet-stream";
    
    /// <summary>
    /// 文件夹（可选）
    /// </summary>
    [JsonPropertyName("folder")]
    public string? Folder { get; set; }
}

/// <summary>
/// 预签名上传 URL 响应
/// </summary>
public class PresignedUploadResponse
{
    /// <summary>
    /// 上传 URL
    /// S3 模式：预签名 URL，客户端直接 PUT 到此 URL
    /// Local 模式：服务端上传端点
    /// </summary>
    [JsonPropertyName("uploadUrl")]
    public string UploadUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// 文件访问 URL
    /// </summary>
    [JsonPropertyName("fileUrl")]
    public string FileUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// 对象键
    /// </summary>
    [JsonPropertyName("objectKey")]
    public string ObjectKey { get; set; } = string.Empty;
    
    /// <summary>
    /// 过期时间（UTC）
    /// </summary>
    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }
    
    /// <summary>
    /// 上传方式: Direct（直接上传到存储）, Server（通过服务端中转）
    /// 客户端根据此字段选择上传方式
    /// </summary>
    [JsonPropertyName("uploadMethod")]
    public string UploadMethod { get; set; } = "Direct";
}
