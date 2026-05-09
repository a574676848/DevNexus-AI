using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 解析代码请求
/// </summary>
public class ParseCodeRequest
{
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
}
