using DevNexus.Domain.Entities.Base;
using DevNexus.Shared.Enums;

namespace DevNexus.Domain.Entities;

/// <summary>
/// 文件任务实体
/// </summary>
public class FileTask : AuditableEntity
{
    /// <summary>
    /// 所属会话 ID
    /// </summary>
    public Guid? SessionId { get; set; }

    /// <summary>
    /// 任务类型
    /// </summary>
    public string TaskType { get; set; } = string.Empty;

    /// <summary>
    /// 输入资产 ID 列表
    /// </summary>
    public List<Guid> InputAssetIds { get; set; } = new();

    /// <summary>
    /// 模板资产 ID 列表
    /// </summary>
    public List<Guid> TemplateAssetIds { get; set; } = new();

    /// <summary>
    /// 输出资产 ID 列表
    /// </summary>
    public List<Guid> OutputAssetIds { get; set; } = new();

    /// <summary>
    /// 任务目录路径
    /// </summary>
    public string? TaskDirectoryPath { get; set; }

    /// <summary>
    /// 附加指令
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public FileTaskStatus Status { get; set; } = FileTaskStatus.Pending;

    /// <summary>
    /// 当前阶段
    /// </summary>
    public FileTaskStage Stage { get; set; } = FileTaskStage.Queued;

    /// <summary>
    /// 当前阶段摘要
    /// </summary>
    public string? StageSummary { get; set; }

    /// <summary>
    /// 错误摘要
    /// </summary>
    public string? ErrorSummary { get; set; }
}