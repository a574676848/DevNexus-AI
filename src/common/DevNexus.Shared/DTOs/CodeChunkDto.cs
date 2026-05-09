using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 代码块 DTO
/// </summary>
public class CodeChunkDto
{
    /// <summary>
    /// 唯一标识
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 代码块类型 (Class, Method, Property, Interface, Enum 等)
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 代码块名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 完整代码
    /// </summary>
    [JsonPropertyName("fullCode")]
    public string FullCode { get; set; } = string.Empty;

    /// <summary>
    /// 摘要（从 XML 注释提取）
    /// </summary>
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// 起始行号
    /// </summary>
    [JsonPropertyName("startLine")]
    public int StartLine { get; set; }

    /// <summary>
    /// 结束行号
    /// </summary>
    [JsonPropertyName("endLine")]
    public int EndLine { get; set; }

    /// <summary>
    /// 元数据
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}
