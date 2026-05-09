using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 文件任务意图判定响应
/// </summary>
public class FileTaskIntentDecisionResponse
{
    /// <summary>
    /// 是否应创建文件任务
    /// </summary>
    [JsonPropertyName("shouldCreateFileTask")]
    public bool ShouldCreateFileTask { get; set; }

    /// <summary>
    /// 建议任务类型
    /// </summary>
    [JsonPropertyName("taskType")]
    public string TaskType { get; set; } = "chat-file-orchestration";

    /// <summary>
    /// 置信度（0-1）
    /// </summary>
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    /// <summary>
    /// 判定原因
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    /// <summary>
    /// 判定来源（llm/fallback）
    /// </summary>
    [JsonPropertyName("decisionSource")]
    public string DecisionSource { get; set; } = "llm";
}
