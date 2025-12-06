using DevNexus.Infrastructure.Models.Base;

namespace DevNexus.Infrastructure.Models;

/// <summary>
/// 聊天会话实体
/// </summary>
public class ChatSession : AuditableEntity
{
    /// <summary>
    /// 会话标题
    /// </summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// 用户ID
    /// </summary>
    public Guid UserId { get; set; }
    
    /// <summary>
    /// 关联的用户
    /// </summary>
    public User User { get; set; } = null!;
    
    /// <summary>
    /// 会话中的消息列表
    /// </summary>
    public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    
    /// <summary>
    /// 是否为活跃会话
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// 会话元数据
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; } = null;
}
