using DevNexus.Domain.Entities.Base;

namespace DevNexus.Domain.Entities;

/// <summary>
/// 文件版本实体
/// </summary>
public class FileVersion : AuditableEntity
{
    /// <summary>
    /// 文件资产 ID
    /// </summary>
    public Guid FileAssetId { get; set; }

    /// <summary>
    /// 版本号
    /// </summary>
    public int VersionNumber { get; set; } = 1;

    /// <summary>
    /// 父版本 ID
    /// </summary>
    public Guid? ParentVersionId { get; set; }

    /// <summary>
    /// 存储对象键
    /// </summary>
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>
    /// 文件访问 URL
    /// </summary>
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// 来源任务 ID
    /// </summary>
    public Guid? GeneratedByTaskId { get; set; }

    /// <summary>
    /// 变更摘要
    /// </summary>
    public string? ChangeSummary { get; set; }
}