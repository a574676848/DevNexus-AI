using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 文件任务意图判定请求
/// </summary>
public class FileTaskIntentDecisionRequest
{
    /// <summary>
    /// 用户输入的指令文本
    /// </summary>
    [JsonPropertyName("instructions")]
    public string? Instructions { get; set; }

    /// <summary>
    /// 候选输入文件资产 ID 列表
    /// </summary>
    [JsonPropertyName("inputAssetIds")]
    public List<Guid> InputAssetIds { get; set; } = new();

    /// <summary>
    /// 指定的 LLM Provider ID（可选，不传则走默认 Provider）
    /// </summary>
    [JsonPropertyName("llmProviderId")]
    public Guid? LLMProviderId { get; set; }
}
