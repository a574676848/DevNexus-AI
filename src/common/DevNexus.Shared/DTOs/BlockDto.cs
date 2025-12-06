using System.Text.Json.Serialization;
using DevNexus.Shared.Enums;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 区块DTO，用于服务器向客户端发送区块数据
/// </summary>
public class BlockDto
{
    /// <summary>
    /// 区块ID
    /// </summary>
    [JsonPropertyName("blockId")]
    public Guid BlockId { get; set; } = Guid.NewGuid();
    
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
}
