using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 完成上传响应
/// </summary>
public class FinalizeUploadResponse
{
    /// <summary>
    /// 上传会话
    /// </summary>
    [JsonPropertyName("uploadSession")]
    public UploadSessionDto UploadSession { get; set; } = new();

    /// <summary>
    /// 文件资产
    /// </summary>
    [JsonPropertyName("fileAsset")]
    public FileAssetDto FileAsset { get; set; } = new();
}