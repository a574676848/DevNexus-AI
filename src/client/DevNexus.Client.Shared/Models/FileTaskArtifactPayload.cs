using DevNexus.Shared.Enums;

namespace DevNexus.Client.Shared.Models;

/// <summary>
/// 文件任务交互卡片负载。
/// </summary>
public class FileTaskArtifactPayload
{
    /// <summary>
    /// 文件任务 ID。
    /// </summary>
    public Guid FileTaskId { get; set; }

    /// <summary>
    /// 任务类型。
    /// </summary>
    public string TaskType { get; set; } = string.Empty;

    /// <summary>
    /// 任务状态。
    /// </summary>
    public FileTaskStatus Status { get; set; } = FileTaskStatus.Pending;

    /// <summary>
    /// 当前任务阶段。
    /// </summary>
    public FileTaskStage Stage { get; set; } = FileTaskStage.Queued;

    /// <summary>
    /// 当前阶段摘要。
    /// </summary>
    public string? StageSummary { get; set; }

    /// <summary>
    /// 用户指令。
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// 输入文件列表。
    /// </summary>
    public List<FileTaskArtifactItem> InputFiles { get; set; } = new();

    /// <summary>
    /// 模板文件列表。
    /// </summary>
    public List<FileTaskArtifactItem> TemplateFiles { get; set; } = new();

    /// <summary>
    /// 输出文件数量。
    /// </summary>
    public int OutputCount { get; set; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间。
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 文件任务卡片中的文件项。
/// </summary>
public class FileTaskArtifactItem
{
    /// <summary>
    /// 文件资产 ID。
    /// </summary>
    public Guid? FileAssetId { get; set; }

    /// <summary>
    /// 文件名。
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 能力标签。
    /// </summary>
    public string CapabilityTag { get; set; } = string.Empty;

    /// <summary>
    /// 是否已可执行。
    /// </summary>
    public bool IsExecutableReady { get; set; }
}