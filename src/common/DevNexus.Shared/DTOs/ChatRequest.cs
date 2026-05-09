using System.Text.Json.Serialization;
using DevNexus.Shared.Constants;

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
    public string MessageType { get; set; } = ChatConstants.MessageTypeText;
    
    /// <summary>
    /// 上下文ID列表
    /// </summary>
    [JsonPropertyName("contextIds")]
    public List<Guid>? ContextIds { get; set; }
    
    /// <summary>
    /// 用户选择的 LLM Provider ID
    /// </summary>
    [JsonPropertyName("llmProviderId")]
    public Guid? LLMProviderId { get; set; }
    
    /// <summary>
    /// 附带的 Artifact ID 列表(发送消息时上传的文档/代码片段)
    /// 后端将在创建消息后更新这些 Artifact 的 MessageId，
    /// 并将其作为本轮“活跃文档”优先注入上下文。
    /// </summary>
    [JsonPropertyName("artifactIds")]
    public List<Guid>? ArtifactIds { get; set; }
    
    /// <summary>
    /// 是否启用 RAG(检索增强生成)
    /// true: 启用知识库检索与文档索引补充(默认)
    /// false: 不进行知识库检索，仅使用活跃附件上下文 + LLM 回复
    /// </summary>
    [JsonPropertyName("enableRag")]
    public bool EnableRag { get; set; } = true;
    
    /// <summary>
    /// 是否启用 Swarm 自动评估
    /// true: 复杂请求自动升级为集群模式 (默认)
    /// false: 始终使用单 Agent 流式回复
    /// </summary>
    [JsonPropertyName("enableSwarm")]
    public bool EnableSwarm { get; set; } = true;
    
    /// <summary>
    /// 是否为续写请求（max_tokens 截断后继续生成）
    /// </summary>
    [JsonPropertyName("isContinuation")]
    public bool IsContinuation { get; set; }

    /// <summary>
    /// 续写的原始消息ID（截断的那条 AI 消息）
    /// </summary>
    [JsonPropertyName("continuationMessageId")]
    public Guid? ContinuationMessageId { get; set; }

    /// <summary>
    /// 附加数据
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// 显式指定的 Skill 名称
    /// 当用户通过 / 指令主动选择 Skill 时，后端应优先激活该 Skill。
    /// </summary>
    [JsonPropertyName("selectedSkillName")]
    public string? SelectedSkillName { get; set; }
}
