using DevNexus.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 聊天会话删除协调器。
/// 负责组织事务内主数据删除与事务外补偿清理。
/// </summary>
public interface IChatSessionDeletionCoordinator
{
    /// <summary>
    /// 删除聊天会话并执行必要的补偿清理。
    /// </summary>
    Task DeleteAsync(
        ChatSession session,
        Guid userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 聊天会话删除协调器实现。
/// </summary>
internal sealed class ChatSessionDeletionCoordinator : IChatSessionDeletionCoordinator
{
    private readonly IChatSessionCleanupCoordinator _cleanupCoordinator;
    private readonly IArtifactRepository _artifactRepository;
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly IChatSessionRepository _chatSessionRepository;
    private readonly ISessionMemoryService _sessionMemoryService;
    private readonly ChatSearchService _chatSearchService;
    private readonly IExecutionStrategyExecutor _executionStrategyExecutor;
    private readonly IUnitOfWorkTransactionFactory _unitOfWorkTransactionFactory;
    private readonly ILogger<ChatSessionDeletionCoordinator> _logger;

    public ChatSessionDeletionCoordinator(
        IChatSessionCleanupCoordinator cleanupCoordinator,
        IArtifactRepository artifactRepository,
        IChatMessageRepository chatMessageRepository,
        IChatSessionRepository chatSessionRepository,
        ISessionMemoryService sessionMemoryService,
        ChatSearchService chatSearchService,
        IExecutionStrategyExecutor executionStrategyExecutor,
        IUnitOfWorkTransactionFactory unitOfWorkTransactionFactory,
        ILogger<ChatSessionDeletionCoordinator> logger)
    {
        _cleanupCoordinator = cleanupCoordinator;
        _artifactRepository = artifactRepository;
        _chatMessageRepository = chatMessageRepository;
        _chatSessionRepository = chatSessionRepository;
        _sessionMemoryService = sessionMemoryService;
        _chatSearchService = chatSearchService;
        _executionStrategyExecutor = executionStrategyExecutor;
        _unitOfWorkTransactionFactory = unitOfWorkTransactionFactory;
        _logger = logger;
    }

    public async Task DeleteAsync(
        ChatSession session,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _cleanupCoordinator.CleanupForSessionDeletionAsync(session, userId, cancellationToken);

        var messages = await _chatMessageRepository.ListBySessionAsync(session.Id, cancellationToken);

        await _executionStrategyExecutor.ExecuteAsync(async ct =>
        {
            await using var transaction = await _unitOfWorkTransactionFactory.BeginTransactionAsync(ct);

            await _artifactRepository.DeleteBySessionIdAsync(session.Id, ct);

            if (messages.Any())
            {
                await _chatMessageRepository.DeleteRangeAsync(messages, ct);
            }

            await _chatSessionRepository.DeleteAsync(session, ct);
            await transaction.CommitAsync(ct);
            return true;
        }, cancellationToken);

        try
        {
            await _sessionMemoryService.DeleteAllAsync(userId.ToString(), session.Id.ToString(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete session memory for session {SessionId}", session.Id);
        }

        await _chatSearchService.DeleteSessionFromElasticsearchAsync(session.Id, cancellationToken);

        _logger.LogInformation(
            "Chat session {SessionId} deleted with {MessageCount} messages",
            session.Id,
            messages.Count);
    }
}
