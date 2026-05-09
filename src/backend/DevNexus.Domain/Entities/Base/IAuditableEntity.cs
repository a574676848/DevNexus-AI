namespace DevNexus.Domain.Entities.Base;

/// <summary>
/// 可审计实体接口
/// </summary>
public interface IAuditableEntity
{
    /// <summary>
    /// 创建时间
    /// </summary>
    DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// 更新时间
    /// </summary>
    DateTime UpdatedAt { get; set; }
}
