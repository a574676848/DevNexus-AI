using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 聊天会话DTO
/// </summary>
public class ChatSessionDto
{
    /// <summary>
    /// 会话ID
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
    
    /// <summary>
    /// 会话标题
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    
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
    /// 是否为活跃会话
    /// </summary>
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }
    
    /// <summary>
    /// 消息数量
    /// </summary>
    [JsonPropertyName("messageCount")]
    public int MessageCount { get; set; }
    
    /// <summary>
    /// 用户选择的 LLM Provider ID
    /// </summary>
    [JsonPropertyName("llmProviderId")]
    public Guid? LLMProviderId { get; set; }
    
    /// <summary>
    /// 关联的 LLM 供应商名称
    /// </summary>
    [JsonPropertyName("llmProviderName")]
    public string? LLMProviderName { get; set; }
    
    /// <summary>
    /// 最后一条消息
    /// </summary>
    [JsonPropertyName("lastMessage")]
    public ChatMessageDto? LastMessage { get; set; }
}
