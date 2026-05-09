using DevNexus.Domain.Entities.Base;
using DevNexus.Shared.Enums;

namespace DevNexus.Domain.Entities;

/// <summary>
/// 文件资产实体
/// </summary>
public class FileAsset : AuditableEntity
{
    /// <summary>
    /// 所属会话 ID
    /// </summary>
    public Guid? SessionId { get; set; }

    /// <summary>
    /// 当前版本 ID
    /// </summary>
    public Guid CurrentVersionId { get; set; }

    /// <summary>
    /// 原始文件名
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// 文件扩展名
    /// </summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>
    /// 内容类型
    /// </summary>
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>
    /// 存储提供商
    /// </summary>
    public string StorageProvider { get; set; } = string.Empty;

    /// <summary>
    /// 文件访问 URL
    /// </summary>
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>
    /// 存储对象键
    /// </summary>
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public FileAssetStatus Status { get; set; } = FileAssetStatus.PendingUpload;

    /// <summary>
    /// 来源类型
    /// </summary>
    public string SourceType { get; set; } = "chat-upload";

    /// <summary>
    /// 扩展元数据
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}