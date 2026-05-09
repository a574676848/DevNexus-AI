using DevNexus.Domain.Entities.Base;

namespace DevNexus.Domain.Entities;

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
    /// 会话中的消息列表
    /// </summary>
    public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    
    /// <summary>
    /// 是否为活跃会话
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// 用户选择的 LLM 供应商 ID
    /// </summary>
    public Guid? LLMProviderId { get; set; }
    
    /// <summary>
    /// 关联的 LLM 供应商
    /// </summary>
    public LLMProvider? LLMProvider { get; set; }
    
    /// <summary>
    /// 会话元数据
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; } = null;

    /// <summary>
    /// 上次记忆沉淀时的消息数量（用于增量判断）
    /// </summary>
    public int LastConsolidatedMessageCount { get; set; } = 0;
    
    /// <summary>
    /// 上次记忆沉淀时间
    /// </summary>
    public DateTime? LastConsolidatedAt { get; set; }
    
    /// <summary>
    /// 当前挂起的延迟记忆沉淀任务ID（用于取消重复调度）
    /// </summary>
    public string? MemoryConsolidationJobId { get; set; }
}
