using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 索引代码请求
/// </summary>
public class IndexCodeRequest
{
    /// <summary>
    /// 代码库ID
    /// </summary>
    [JsonPropertyName("repositoryId")]
    public string RepositoryId { get; set; } = string.Empty;

    /// <summary>
    /// 源代码
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 编程语言
    /// </summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = "csharp";

    /// <summary>
    /// 文件路径（可选）
    /// </summary>
    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }
}
