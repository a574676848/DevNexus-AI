using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 创建上传会话请求
/// </summary>
public class CreateUploadSessionRequest
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
    /// 会话 ID
    /// </summary>
    [JsonPropertyName("sessionId")]
    public Guid? SessionId { get; set; }

    /// <summary>
    /// 预期文件大小
    /// </summary>
    [JsonPropertyName("expectedSizeBytes")]
    public long? ExpectedSizeBytes { get; set; }

    /// <summary>
    /// 来源类型
    /// </summary>
    [JsonPropertyName("sourceType")]
    public string SourceType { get; set; } = "chat-upload";
}