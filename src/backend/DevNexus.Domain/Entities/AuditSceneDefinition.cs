using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DevNexus.Domain.Entities.Base;

namespace DevNexus.Domain.Entities;

/// <summary>
/// 审计场景字典定义。
/// </summary>
[Table("AuditSceneDefinitions")]
public class AuditSceneDefinition : AuditableEntity
{
    /// <summary>
    /// 场景编码。
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string SceneCode { get; set; } = string.Empty;

    /// <summary>
    /// 中文名称。
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string DisplayNameZhCn { get; set; } = string.Empty;

    /// <summary>
    /// 中文短名称。
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ShortNameZhCn { get; set; } = string.Empty;

    /// <summary>
    /// 中文说明。
    /// </summary>
    [MaxLength(500)]
    public string? DescriptionZhCn { get; set; }

    /// <summary>
    /// 中文分组。
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string DisplayGroupZhCn { get; set; } = string.Empty;

    /// <summary>
    /// 徽标色调。
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string BadgeTone { get; set; } = "neutral";

    /// <summary>
    /// 是否系统场景。
    /// </summary>
    public bool IsSystemScene { get; set; }

    /// <summary>
    /// 排序值。
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}
