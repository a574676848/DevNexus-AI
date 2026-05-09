using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 搜索代码请求
/// </summary>
public class SearchCodeRequest
{
    /// <summary>
    /// 查询内容
    /// </summary>
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// 代码库ID（可选）
    /// </summary>
    [JsonPropertyName("repositoryId")]
    public string? RepositoryId { get; set; }

    /// <summary>
    /// 返回结果数量
    /// </summary>
    [JsonPropertyName("topK")]
    public int TopK { get; set; } = 5;
}
