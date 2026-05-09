using System.Text.Json.Serialization;
using DevNexus.Shared.Enums;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 上传会话 DTO
/// </summary>
public class UploadSessionDto
{
    /// <summary>
    /// 上传会话 ID
    /// </summary>
    [JsonPropertyName("uploadSessionId")]
    public Guid UploadSessionId { get; set; }

    /// <summary>
    /// 关联文件资产 ID
    /// </summary>
    [JsonPropertyName("fileAssetId")]
    public Guid FileAssetId { get; set; }

    /// <summary>
    /// 所属会话 ID
    /// </summary>
    [JsonPropertyName("sessionId")]
    public Guid? SessionId { get; set; }

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
    /// 上传地址
    /// </summary>
    [JsonPropertyName("uploadUrl")]
    public string UploadUrl { get; set; } = string.Empty;

    /// <summary>
    /// 上传方式
    /// </summary>
    [JsonPropertyName("uploadMethod")]
    public string UploadMethod { get; set; } = "Direct";

    /// <summary>
    /// 状态
    /// </summary>
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UploadSessionStatus Status { get; set; } = UploadSessionStatus.Created;

    /// <summary>
    /// 预期文件大小
    /// </summary>
    [JsonPropertyName("expectedSizeBytes")]
    public long? ExpectedSizeBytes { get; set; }

    /// <summary>
    /// 过期时间
    /// </summary>
    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }

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