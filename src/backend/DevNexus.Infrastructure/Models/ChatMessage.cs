using DevNexus.Infrastructure.Models.Base;

namespace DevNexus.Infrastructure.Models;

/// <summary>
/// 聊天消息实体
/// </summary>
public class ChatMessage : AuditableEntity
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public Guid ChatSessionId { get; set; }
    
    /// <summary>
    /// 关联的会话
    /// </summary>
    public ChatSession ChatSession { get; set; } = null!;
    
    /// <summary>
    /// 父消息ID
    /// </summary>
    public Guid? ParentMessageId { get; set; } = null;
    
    /// <summary>
    /// 父消息
    /// </summary>
    public ChatMessage? ParentMessage { get; set; } = null;
    
    /// <summary>
    /// 子消息列表
    /// </summary>
    public List<ChatMessage> ChildMessages { get; set; } = new List<ChatMessage>();
    
    /// <summary>
    /// 发送者ID
    /// </summary>
    public Guid SenderId { get; set; }
    
    /// <summary>
    /// 发送者类型
    /// </summary>
    public string SenderType { get; set; } = "user"; // user 或 assistant
    
    /// <summary>
    /// 消息内容（JSONB格式）
    /// </summary>
    public Dictionary<string, object> Content { get; set; } = new Dictionary<string, object>();
    
    /// <summary>
    /// 消息类型
    /// </summary>
    public string MessageType { get; set; } = "text";
    
    /// <summary>
    /// 消息元数据（JSONB格式）
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; } = null;
    
    /// <summary>
    /// 消息状态
    /// </summary>
    public string Status { get; set; } = "completed"; // pending, in_progress, completed, cancelled
}
