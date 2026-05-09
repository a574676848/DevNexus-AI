using System.Text.Json.Serialization;
using DevNexus.Shared.Enums;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 区块DTO，用于服务器向客户端发送区块数据
/// </summary>
public class BlockDto
{
    /// <summary>
    /// 区块ID（系统生成的唯一标识）
    /// </summary>
    [JsonPropertyName("blockId")]
    public Guid BlockId { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Artifact 语义标识符（由 LLM 指定，用于引用和增量更新）
    /// 例如: "user-service", "main-controller"
    /// </summary>
    [JsonPropertyName("artifactId")]
    public string? ArtifactId { get; set; }
    
    /// <summary>
    /// 版本号（用于增量更新追踪）
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;
    
    /// <summary>
    /// 操作类型（Create/Update/Delete）
    /// </summary>
    [JsonPropertyName("action")]
    public BlockAction Action { get; set; } = BlockAction.Create;
    
    /// <summary>
    /// 会话ID（用于多会话并行分发）
    /// </summary>
    [JsonPropertyName("sessionId")]
    public Guid SessionId { get; set; }
    
    /// <summary>
    /// 区块类型
    /// </summary>
    [JsonPropertyName("blockType")]
    public BlockType BlockType { get; set; }
    
    /// <summary>
    /// 区块内容
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// 区块元数据
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
    
    /// <summary>
    /// 消息ID
    /// </summary>
    [JsonPropertyName("messageId")]
    public Guid MessageId { get; set; }
    
    /// <summary>
    /// 是否为最后一个区块
    /// </summary>
    [JsonPropertyName("isLast")]
    public bool IsLast { get; set; }
    
    /// <summary>
    /// 高亮行号范围（格式: "5-8,12,20-25"）
    /// </summary>
    [JsonPropertyName("highlight")]
    public string? Highlight { get; set; }
}
