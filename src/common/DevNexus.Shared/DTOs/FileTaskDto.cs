using System.Text.Json.Serialization;
using DevNexus.Shared.Enums;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 文件任务 DTO
/// </summary>
public class FileTaskDto
{
    /// <summary>
    /// 文件任务 ID
    /// </summary>
    [JsonPropertyName("fileTaskId")]
    public Guid FileTaskId { get; set; }

    /// <summary>
    /// 所属会话 ID
    /// </summary>
    [JsonPropertyName("sessionId")]
    public Guid? SessionId { get; set; }

    /// <summary>
    /// 任务类型
    /// </summary>
    [JsonPropertyName("taskType")]
    public string TaskType { get; set; } = string.Empty;

    /// <summary>
    /// 输入资产 ID 列表
    /// </summary>
    [JsonPropertyName("inputAssetIds")]
    public List<Guid> InputAssetIds { get; set; } = new();

    /// <summary>
    /// 模板资产 ID 列表
    /// </summary>
    [JsonPropertyName("templateAssetIds")]
    public List<Guid> TemplateAssetIds { get; set; } = new();

    /// <summary>
    /// 输出资产 ID 列表
    /// </summary>
    [JsonPropertyName("outputAssetIds")]
    public List<Guid> OutputAssetIds { get; set; } = new();

    /// <summary>
    /// 任务状态
    /// </summary>
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FileTaskStatus Status { get; set; } = FileTaskStatus.Pending;

    /// <summary>
    /// 当前任务阶段
    /// </summary>
    [JsonPropertyName("stage")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FileTaskStage Stage { get; set; } = FileTaskStage.Queued;

    /// <summary>
    /// 当前阶段摘要
    /// </summary>
    [JsonPropertyName("stageSummary")]
    public string? StageSummary { get; set; }

    /// <summary>
    /// 附加指令
    /// </summary>
    [JsonPropertyName("instructions")]
    public string? Instructions { get; set; }

    /// <summary>
    /// 错误摘要
    /// </summary>
    [JsonPropertyName("errorSummary")]
    public string? ErrorSummary { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}