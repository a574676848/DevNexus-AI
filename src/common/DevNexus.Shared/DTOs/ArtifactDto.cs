using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 文档资产DTO，用于表示独立文档资产
/// </summary>
public class ArtifactDto
{
    /// <summary>
    /// 资产ID
    /// </summary>
    [JsonPropertyName("artifactId")]
    public Guid ArtifactId { get; set; }
    
    /// <summary>
    /// 资产类型
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// 资产名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 资产内容
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// 父资产ID
    /// </summary>
    [JsonPropertyName("parentArtifactId")]
    public Guid? ParentArtifactId { get; set; }
    
    /// <summary>
    /// 消息ID
    /// </summary>
    [JsonPropertyName("messageId")]
    public Guid MessageId { get; set; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 更新时间
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
