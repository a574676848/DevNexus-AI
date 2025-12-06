using DevNexus.Infrastructure.Models.Base;

namespace DevNexus.Infrastructure.Models;

/// <summary>
/// 文档资产实体
/// </summary>
public class Artifact : AuditableEntity
{
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
    /// 父资产ID
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
    /// 资产状态
    /// </summary>
    public string Status { get; set; } = "active"; // draft, active, archived
    
    /// <summary>
    /// 资产元数据（JSONB格式）
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; } = null;
    
    /// <summary>
    /// 资产大小（字节）
    /// </summary>
    public long Size { get; set; } = 0;
}
