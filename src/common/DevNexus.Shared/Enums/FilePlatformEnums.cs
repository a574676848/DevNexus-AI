namespace DevNexus.Shared.Enums;

/// <summary>
/// 文件资产状态
/// </summary>
public enum FileAssetStatus
{
    PendingUpload,
    Uploaded,
    Processing,
    Ready,
    Failed,
    Archived
}

/// <summary>
/// 上传会话状态
/// </summary>
public enum UploadSessionStatus
{
    Created,
    Uploading,
    Uploaded,
    Finalized,
    Expired,
    Failed
}

/// <summary>
/// 文件派生物状态
/// </summary>
public enum FileDerivativeStatus
{
    Pending,
    Building,
    Ready,
    Failed
}

/// <summary>
/// 文件任务状态
/// </summary>
public enum FileTaskStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// 文件任务阶段
/// </summary>
public enum FileTaskStage
{
    Queued,
    PreparingTaskDirectory,
    StagingAssets,
    ExecutingScript,
    ValidatingOutputs,
    PublishingOutputs,
    Completed,
    Failed,
    Cancelled
}