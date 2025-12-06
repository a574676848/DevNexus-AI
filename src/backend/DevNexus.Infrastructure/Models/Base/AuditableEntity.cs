namespace DevNexus.Infrastructure.Models.Base;

/// <summary>
/// 包含审计字段的基础实体类
/// </summary>
public abstract class AuditableEntity : ISoftDelete
{
    /// <summary>
    /// 实体ID
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 创建者ID
    /// </summary>
    public Guid? CreatedBy { get; set; } = null;
    
    /// <summary>
    /// 更新者ID
    /// </summary>
    public Guid? UpdatedBy { get; set; } = null;
    
    /// <summary>
    /// 是否已删除
    /// </summary>
    public bool IsDeleted { get; set; } = false;
    
    /// <summary>
    /// 删除时间
    /// </summary>
    public DateTime? DeletedAt { get; set; } = null;
}
