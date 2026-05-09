using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 文档资产DTO，用于表示独立文档资产
/// </summary>
public class ArtifactDto
{
    /// <summary>
    /// 资产ID（系统生成的 GUID）
    /// </summary>
    [JsonPropertyName("artifactId")]
    public Guid ArtifactId { get; set; }
    
    /// <summary>
    /// 语义标识符（由 LLM 指定，用于引用和增量更新）
    /// 例如: "user-service", "main-controller"
    /// </summary>
    [JsonPropertyName("semanticId")]
    public string? SemanticId { get; set; }
    
    /// <summary>
    /// 版本号
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;
    
    /// <summary>
    /// 基准版本号（增量更新时的前置版本）
    /// </summary>
    [JsonPropertyName("baseVersion")]
    public int? BaseVersion { get; set; }
    
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
    /// 关联的文件资产 ID
    /// </summary>
    [JsonPropertyName("fileAssetId")]
    public Guid? FileAssetId { get; set; }

    /// <summary>
    /// 关联的文件版本 ID
    /// </summary>
    [JsonPropertyName("fileVersionId")]
    public Guid? FileVersionId { get; set; }
    
    /// <summary>
    /// 父资产ID（用于版本链）
    /// </summary>
    [JsonPropertyName("parentArtifactId")]
    public Guid? ParentArtifactId { get; set; }
    
    /// <summary>
    /// 消息ID
    /// </summary>
    [JsonPropertyName("messageId")]
    public Guid MessageId { get; set; }
    
    /// <summary>
    /// 会话ID
    /// </summary>
    [JsonPropertyName("sessionId")]
    public Guid? SessionId { get; set; }
    
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
    
    /// <summary>
    /// 元数据（用于存储卡片状态等扩展信息）
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}
