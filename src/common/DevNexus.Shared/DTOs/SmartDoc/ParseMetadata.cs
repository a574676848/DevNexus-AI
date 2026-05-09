using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 解析元数据 - 记录解析策略、耗时、成本等信息，用于监控和调试
/// </summary>
public class ParseMetadata
{
    /// <summary>
    /// 解析策略，例如: "vision", "roslyn", "regex", "client-csv", "pdfpig"
    /// </summary>
    [JsonPropertyName("strategy")]
    public string Strategy { get; set; } = string.Empty;

    /// <summary>
    /// 解析耗时
    /// </summary>
    [JsonPropertyName("processingTimeMs")]
    public double ProcessingTimeMs { get; set; }

    /// <summary>
    /// 估算成本 (美元)，仅 Vision/LLM 调用时有值
    /// </summary>
    [JsonPropertyName("costUSD")]
    public decimal? CostUSD { get; set; }

    /// <summary>
    /// 解析质量评分 (0.0-1.0)
    /// </summary>
    [JsonPropertyName("qualityScore")]
    public double QualityScore { get; set; } = 1.0;

    /// <summary>
    /// 警告信息列表，例如: "使用了 OCR fallback", "表格可能不完整"
    /// </summary>
    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// 使用的模型名称，例如: "gpt-4-vision-preview", "gemini-2.0-flash"
    /// </summary>
    [JsonPropertyName("modelUsed")]
    public string? ModelUsed { get; set; }

    /// <summary>
    /// 消耗的 Token 数 (Vision/LLM 调用时)
    /// </summary>
    [JsonPropertyName("tokensUsed")]
    public int? TokensUsed { get; set; }

    /// <summary>
    /// 解析位置: "server" 或 "client"
    /// </summary>
    [JsonPropertyName("parsedBy")]
    public string ParsedBy { get; set; } = "server";
}
