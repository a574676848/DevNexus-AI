using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 聊天消息DTO
/// </summary>
public class ChatMessageDto
{
    /// <summary>
    /// 消息ID
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
    
    /// <summary>
    /// 会话ID
    /// </summary>
    [JsonPropertyName("chatSessionId")]
    public Guid ChatSessionId { get; set; }
    
    /// <summary>
    /// 父消息ID
    /// </summary>
    [JsonPropertyName("parentMessageId")]
    public Guid? ParentMessageId { get; set; }
    
    /// <summary>
    /// 发送者ID
    /// </summary>
    [JsonPropertyName("senderId")]
    public Guid SenderId { get; set; }
    
    /// <summary>
    /// 发送者类型
    /// </summary>
    [JsonPropertyName("senderType")]
    public string SenderType { get; set; } = "user";
    
    /// <summary>
    /// 消息内容
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// 消息类型
    /// </summary>
    [JsonPropertyName("messageType")]
    public string MessageType { get; set; } = "text";
    
    /// <summary>
    /// 消息状态
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "completed";
    
    /// <summary>
    /// 创建时间
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// 更新时间
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }
    
    /// <summary>
    /// 附加数据
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
    
    /// <summary>
    /// 子消息
    /// </summary>
    [JsonPropertyName("childMessages")]
    public List<ChatMessageDto>? ChildMessages { get; set; }
}
