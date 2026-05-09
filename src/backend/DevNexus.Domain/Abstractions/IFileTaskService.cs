using DevNexus.Shared.DTOs;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 文件任务服务接口
/// </summary>
public interface IFileTaskService
{
    /// <summary>
    /// 判定是否应创建文件任务
    /// </summary>
    Task<FileTaskIntentDecisionResponse> DecideFileTaskIntentAsync(
        Guid userId,
        FileTaskIntentDecisionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建文件任务
    /// </summary>
    Task<FileTaskDto> CreateFileTaskAsync(
        Guid userId,
        CreateFileTaskRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取文件任务
    /// </summary>
    Task<FileTaskDto?> GetFileTaskAsync(
        Guid userId,
        Guid fileTaskId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取会话中的文件任务
    /// </summary>
    Task<IReadOnlyList<FileTaskDto>> GetSessionFileTasksAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 重新执行文件任务
    /// </summary>
    Task<FileTaskDto> RetryFileTaskAsync(
        Guid userId,
        Guid fileTaskId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消文件任务
    /// </summary>
    Task<FileTaskDto> CancelFileTaskAsync(
        Guid userId,
        Guid fileTaskId,
        CancellationToken cancellationToken = default);
}
