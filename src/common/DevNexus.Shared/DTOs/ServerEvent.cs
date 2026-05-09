using System.Text.Json.Serialization;
using DevNexus.Shared.Enums;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 服务器事件DTO，用于服务器向客户端发送事件数据
/// </summary>
public class ServerEvent
{
    /// <summary>
    /// 事件类型
    /// </summary>
    [JsonPropertyName("eventType")]
    public ServerEventType EventType { get; set; }
    
    /// <summary>
    /// 会话ID
    /// </summary>
    [JsonPropertyName("sessionId")]
    public Guid SessionId { get; set; }
    
    /// <summary>
    /// 事件数据
    /// </summary>
    [JsonPropertyName("data")]
    public object? Data { get; set; }
    
    /// <summary>
    /// 事件时间戳
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
