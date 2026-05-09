using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 代码解析结果
/// </summary>
public class CodeParseResultDto
{
    /// <summary>
    /// 代码块列表
    /// </summary>
    [JsonPropertyName("chunks")]
    public List<CodeChunkDto> Chunks { get; set; } = new();

    /// <summary>
    /// 代码块数量
    /// </summary>
    [JsonPropertyName("count")]
    public int Count { get; set; }
}
