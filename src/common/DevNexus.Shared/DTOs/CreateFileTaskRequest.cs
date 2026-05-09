using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 创建文件任务请求
/// </summary>
public class CreateFileTaskRequest
{
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
    /// 附加指令
    /// </summary>
    [JsonPropertyName("instructions")]
    public string? Instructions { get; set; }
}