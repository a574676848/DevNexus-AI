using System.Text.Json.Serialization;
using DevNexus.Shared.Enums;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 工具调用状态通知 DTO
/// </summary>
public class ToolInvocationDto
{
    /// <summary>
    /// 会话 ID
    /// </summary>
    [JsonPropertyName("sessionId")]
    public Guid SessionId { get; set; }

    /// <summary>
    /// 消息 ID
    /// </summary>
    [JsonPropertyName("messageId")]
    public Guid MessageId { get; set; }

    /// <summary>
    /// 工具调用 ID（用于前端替换更新）
    /// </summary>
    [JsonPropertyName("toolCallId")]
    public Guid ToolCallId { get; set; }

    /// <summary>
    /// 插件名称
    /// </summary>
    [JsonPropertyName("pluginName")]
    public string PluginName { get; set; } = string.Empty;

    /// <summary>
    /// 函数名称
    /// </summary>
    [JsonPropertyName("functionName")]
    public string FunctionName { get; set; } = string.Empty;

    /// <summary>
    /// 状态：running/completed/failed/cancelled/timeout 等（见 ToolInvocationStatus）
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = ToolInvocationStatus.Running.ToWireValue();

    /// <summary>
    /// 持续时间（毫秒）
    /// </summary>
    [JsonPropertyName("durationMs")]
    public long? DurationMs { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}
