using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 完成上传请求
/// </summary>
public class FinalizeUploadRequest
{
    /// <summary>
    /// 上传会话 ID
    /// </summary>
    [JsonPropertyName("uploadSessionId")]
    public Guid UploadSessionId { get; set; }

    /// <summary>
    /// 实际文件大小
    /// </summary>
    [JsonPropertyName("sizeBytes")]
    public long? SizeBytes { get; set; }
}