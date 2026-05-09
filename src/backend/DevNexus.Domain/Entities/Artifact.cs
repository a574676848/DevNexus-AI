using DevNexus.Domain.Entities.Base;
using DevNexus.Shared.Enums;

namespace DevNexus.Domain.Entities;

/// <summary>
/// 文档资产实体
/// </summary>
public class Artifact : AuditableEntity
{
    /// <summary>
    /// 语义标识符（由 LLM 指定，用于引用和增量更新）
    /// 例如: "user-service", "main-controller"
    /// </summary>
    public string? SemanticId { get; set; } = null;
    
    /// <summary>
    /// 版本号
    /// </summary>
    public int Version { get; set; } = 1;
    
    /// <summary>
    /// 基准版本号（增量更新时的前置版本）
    /// </summary>
    public int? BaseVersion { get; set; } = null;
    
    /// <summary>
    /// 资产类型
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// 资产名称
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 资产内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 关联的文件资产 ID
    /// </summary>
    public Guid? FileAssetId { get; set; } = null;

    /// <summary>
    /// 关联的文件版本 ID
    /// </summary>
    public Guid? FileVersionId { get; set; } = null;
    
    /// <summary>
    /// 父资产ID（用于版本链）
    /// </summary>
    public Guid? ParentArtifactId { get; set; } = null;
    
    /// <summary>
    /// 父资产
    /// </summary>
    public Artifact? ParentArtifact { get; set; } = null;
    
    /// <summary>
    /// 消息ID
    /// </summary>
    public Guid? MessageId { get; set; } = null;
    
    /// <summary>
    /// 关联的消息
    /// </summary>
    public ChatMessage? Message { get; set; } = null;
    
    /// <summary>
    /// 会话ID（用于会话级 Artifact 查询）
    /// </summary>
    public Guid? SessionId { get; set; } = null;
    
    /// <summary>
    /// 关联的会话
    /// </summary>
    public ChatSession? Session { get; set; } = null;
    
    /// <summary>
    /// 资产状态
    /// </summary>
    public ArtifactLifecycleStatus Status { get; set; } = ArtifactLifecycleStatus.Active;
    
    /// <summary>
    /// 资产元数据（JSONB格式）
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; } = null;
    
    /// <summary>
    /// 资产大小（字节）
    /// </summary>
    public long Size { get; set; } = 0;
}
