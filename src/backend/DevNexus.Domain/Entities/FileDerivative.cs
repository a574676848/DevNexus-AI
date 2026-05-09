using DevNexus.Domain.Entities.Base;
using DevNexus.Shared.Enums;

namespace DevNexus.Domain.Entities;

/// <summary>
/// 文件派生物实体
/// </summary>
public class FileDerivative : AuditableEntity
{
    /// <summary>
    /// 文件资产 ID
    /// </summary>
    public Guid FileAssetId { get; set; }

    /// <summary>
    /// 文件版本 ID
    /// </summary>
    public Guid FileVersionId { get; set; }

    /// <summary>
    /// 派生物类型
    /// </summary>
    public string DerivativeType { get; set; } = string.Empty;

    /// <summary>
    /// 输出格式
    /// </summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// 文本内容
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// 外部存储引用
    /// </summary>
    public string? StorageRef { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public FileDerivativeStatus Status { get; set; } = FileDerivativeStatus.Pending;

    /// <summary>
    /// 元数据
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}