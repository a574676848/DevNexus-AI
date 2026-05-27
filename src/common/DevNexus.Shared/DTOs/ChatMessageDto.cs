using System.Text.Json.Serialization;
using DevNexus.Shared.Constants;

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
    public string SenderType { get; set; } = ChatConstants.RoleUser;
    
    /// <summary>
    /// 消息内容，保留为正文兼容字段。
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 结构化正文内容。
    /// </summary>
    [JsonPropertyName("textContent")]
    public string? TextContent { get; set; }

    /// <summary>
    /// 结构化思考内容。
    /// </summary>
    [JsonPropertyName("thinkingContent")]
    public string? ThinkingContent { get; set; }
    
    /// <summary>
    /// 消息类型
    /// </summary>
    [JsonPropertyName("messageType")]
    public string MessageType { get; set; } = ChatConstants.MessageTypeText;
    
    /// <summary>
    /// 消息状态
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = ChatConstants.StatusCompleted;
    
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

    /// <summary>
    /// 关联的文档资产
    /// </summary>
    [JsonPropertyName("artifacts")]
    public List<ArtifactDto>? Artifacts { get; set; }

    /// <summary>
    /// 图表 Blocks（流式生成的图表数据，用于前端渲染）
    /// </summary>
    [JsonPropertyName("chartBlocks")]
    public List<BlockDto>? ChartBlocks { get; set; }

    /// <summary>
    /// 交互卡片 Blocks（SQL审批、脚本审批、搜索结果、文件操作等）
    /// </summary>
    [JsonPropertyName("interactiveBlocks")]
    public List<BlockDto>? InteractiveBlocks { get; set; }

    /// <summary>
    /// 所有 Block 的原始顺序（包含 Chart、InteractiveCard 等，用于支持交错布局）
    /// 业界主流做法：保留完整的 Block 顺序以实现 "文字 → 卡片 → 更多文字" 的交错渲染
    /// </summary>
    [JsonPropertyName("orderedBlocks")]
    public List<BlockDto>? OrderedBlocks { get; set; }
}
