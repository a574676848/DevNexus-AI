using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 代码搜索结果
/// </summary>
public class SearchCodeResultDto
{
    /// <summary>
    /// 搜索结果
    /// </summary>
    [JsonPropertyName("results")]
    public List<CodeChunkDto> Results { get; set; } = new();

    /// <summary>
    /// 结果数量
    /// </summary>
    [JsonPropertyName("count")]
    public int Count { get; set; }
}
