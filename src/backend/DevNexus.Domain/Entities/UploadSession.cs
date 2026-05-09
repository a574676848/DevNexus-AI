using DevNexus.Domain.Entities.Base;
using DevNexus.Shared.Enums;

namespace DevNexus.Domain.Entities;

/// <summary>
/// 上传会话实体
/// </summary>
public class UploadSession : AuditableEntity
{
    /// <summary>
    /// 文件资产 ID
    /// </summary>
    public Guid FileAssetId { get; set; }

    /// <summary>
    /// 所属会话 ID
    /// </summary>
    public Guid? SessionId { get; set; }

    /// <summary>
    /// 文件名
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 内容类型
    /// </summary>
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>
    /// 文件访问 URL
    /// </summary>
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>
    /// 存储对象键
    /// </summary>
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>
    /// 上传地址
    /// </summary>
    public string UploadUrl { get; set; } = string.Empty;

    /// <summary>
    /// 上传方式
    /// </summary>
    public string UploadMethod { get; set; } = "Direct";

    /// <summary>
    /// 状态
    /// </summary>
    public UploadSessionStatus Status { get; set; } = UploadSessionStatus.Created;

    /// <summary>
    /// 预期大小
    /// </summary>
    public long? ExpectedSizeBytes { get; set; }

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}