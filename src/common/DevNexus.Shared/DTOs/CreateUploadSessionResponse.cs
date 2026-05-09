using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 创建上传会话响应
/// </summary>
public class CreateUploadSessionResponse
{
    /// <summary>
    /// 上传会话
    /// </summary>
    [JsonPropertyName("uploadSession")]
    public UploadSessionDto UploadSession { get; set; } = new();
}