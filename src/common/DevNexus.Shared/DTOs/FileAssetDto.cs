using System.Text.Json.Serialization;
using DevNexus.Shared.Enums;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 文件资产 DTO
/// </summary>
public class FileAssetDto
{
    /// <summary>
    /// 文件资产 ID
    /// </summary>
    [JsonPropertyName("fileAssetId")]
    public Guid FileAssetId { get; set; }

    /// <summary>
    /// 所属会话 ID
    /// </summary>
    [JsonPropertyName("sessionId")]
    public Guid? SessionId { get; set; }

    /// <summary>
    /// 当前版本 ID
    /// </summary>
    [JsonPropertyName("currentVersionId")]
    public Guid CurrentVersionId { get; set; }

    /// <summary>
    /// 原始文件名
    /// </summary>
    [JsonPropertyName("originalFileName")]
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// 文件扩展名
    /// </summary>
    [JsonPropertyName("extension")]
    public string Extension { get; set; } = string.Empty;

    /// <summary>
    /// 内容类型
    /// </summary>
    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>
    /// 存储提供商
    /// </summary>
    [JsonPropertyName("storageProvider")]
    public string StorageProvider { get; set; } = string.Empty;

    /// <summary>
    /// 文件访问 URL
    /// </summary>
    [JsonPropertyName("fileUrl")]
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>
    /// 存储对象键
    /// </summary>
    [JsonPropertyName("objectKey")]
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FileAssetStatus Status { get; set; } = FileAssetStatus.PendingUpload;

    /// <summary>
    /// 文件来源
    /// </summary>
    [JsonPropertyName("sourceType")]
    public string SourceType { get; set; } = "chat-upload";

    /// <summary>
    /// 扩展元数据
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}