using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 解析文档请求
/// 用于需要后端解析的文件类型 (Code/Word/PDF/Image)
/// 注意：此 API 仅负责解析，不创建 Artifact
/// Artifact 的创建由客户端在发送消息时单独调用 CreateArtifact 完成
/// </summary>
public class ParseDocumentRequest
{
    /// <summary>
    /// 文件名
    /// </summary>
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 文件内容 (Base64 编码)
    /// </summary>
    [JsonPropertyName("base64Content")]
    public string Base64Content { get; set; } = string.Empty;

    /// <summary>
    /// 已上传文件的访问 URL（优先使用）
    /// </summary>
    [JsonPropertyName("fileUrl")]
    public string? FileUrl { get; set; }

    /// <summary>
    /// 已登记文件资产 ID（可选）
    /// </summary>
    [JsonPropertyName("fileAssetId")]
    public Guid? FileAssetId { get; set; }

    /// <summary>
    /// MIME 类型
    /// </summary>
    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = "application/octet-stream";

    /// <summary>
    /// 用于 Vision 解析的 LLM 供应商 ID
    /// 直接传递当前选中的供应商，用于图片/PDF Vision 解析
    /// </summary>
    [JsonPropertyName("providerId")]
    public Guid? ProviderId { get; set; }

    /// <summary>
    /// 关联的会话 ID (可选)
    /// </summary>
    [JsonPropertyName("sessionId")]
    public Guid? SessionId { get; set; }
}
