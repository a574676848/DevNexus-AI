using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 聊天请求DTO，用于客户端向服务器发送聊天消息
/// </summary>
public class ChatRequest
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public Guid? SessionId { get; set; }
    
    /// <summary>
    /// 父消息ID，用于构建对话树
    /// </summary>
    public Guid? ParentMessageId { get; set; }
    
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
    /// 上下文ID列表
    /// </summary>
    [JsonPropertyName("contextIds")]
    public List<Guid>? ContextIds { get; set; }
    
    /// <summary>
    /// 附加数据
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}
