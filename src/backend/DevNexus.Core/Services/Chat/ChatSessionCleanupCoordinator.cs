using DevNexus.Domain.Abstractions;
using DevNexus.Core.Services.Swarm.Planning;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Logging;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 聊天会话清理协调器接口。
/// 负责在删除会话前协调取消相关后台任务并清理关联 Swarm 会话。
/// </summary>
public interface IChatSessionCleanupCoordinator
{
    /// <summary>
    /// 清理指定聊天会话关联的后台任务与 Swarm 会话。
    /// </summary>
    Task CleanupForSessionDeletionAsync(
        ChatSession session,
        Guid userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 聊天会话清理协调器。
/// 将会话删除前的跨模块清理逻辑从 ChatService 中下沉，减少主服务职责。
/// </summary>
internal sealed class ChatSessionCleanupCoordinator : IChatSessionCleanupCoordinator
{
    private readonly IFileTaskService _fileTaskService;
    private readonly IContextSwarmSessionRepository _swarmSessionRepository;
    private readonly Swarm.ISwarmSessionControlService _swarmSessionControlService;
    private readonly ILogger<ChatSessionCleanupCoordinator> _logger;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public ChatSessionCleanupCoordinator(
        IFileTaskService fileTaskService,
        IContextSwarmSessionRepository swarmSessionRepository,
        Swarm.ISwarmSessionControlService swarmSessionControlService,
        ILogger<ChatSessionCleanupCoordinator> logger)
    {
        _fileTaskService = fileTaskService;
        _swarmSessionRepository = swarmSessionRepository;
        _swarmSessionControlService = swarmSessionControlService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task CleanupForSessionDeletionAsync(
        ChatSession session,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await CancelSessionFileTasksAsync(session.Id, userId, cancellationToken);
        await CleanupSwarmSessionsAsync(session.Id, cancellationToken);
    }

    /// <summary>
    /// 取消聊天会话关联的文件任务。
    /// </summary>
    private async Task CancelSessionFileTasksAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var sessionFileTasks = await _fileTaskService.GetSessionFileTasksAsync(
            userId,
            sessionId,
            cancellationToken);

        var activeFileTaskIds = sessionFileTasks
            .Where(task => task.Status is FileTaskStatus.Pending or FileTaskStatus.Running)
            .Select(task => task.FileTaskId)
            .ToList();

        foreach (var taskId in activeFileTaskIds)
        {
            try
            {
                await _fileTaskService.CancelFileTaskAsync(userId, taskId, cancellationToken);
            }
            catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
            {
                _logger.LogDebug(
                    ex,
                    "Skip cancelling file task during session deletion | SessionId={SessionId} FileTaskId={FileTaskId}",
                    sessionId,
                    taskId);
            }
        }
    }

    /// <summary>
    /// 清理聊天会话关联的 Swarm 会话与任务。
    /// </summary>
    private async Task CleanupSwarmSessionsAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var sessionIdString = sessionId.ToString();
        var swarmSessions = await _swarmSessionRepository.ListByExternalSessionIdAsync(sessionIdString, cancellationToken);

        if (!swarmSessions.Any())
        {
            return;
        }

        foreach (var swarmSession in swarmSessions)
        {
            try
            {
                await _swarmSessionControlService.AbortAsync(swarmSession.SessionId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to abort Swarm session {SwarmSessionId} when deleting chat session {ChatSessionId}",
                    swarmSession.SessionId,
                    sessionId);
            }
        }

        await _swarmSessionRepository.DeleteRangeAsync(swarmSessions, cancellationToken);
        _logger.LogDebug(
            "Deleted {Count} Swarm sessions associated with chat session {SessionId}",
            swarmSessions.Count,
            sessionId);
    }
}
