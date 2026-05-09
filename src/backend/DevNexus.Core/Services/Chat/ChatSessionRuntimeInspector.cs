using DevNexus.Core.Abstractions;
using DevNexus.Domain.Abstractions;
using DevNexus.Shared.Constants;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 会话统一运行态检查器。
/// 负责聚合挂起交互、CLI、排队和消息状态，并生成统一运行态快照。
/// </summary>
public interface IChatSessionRuntimeInspector
{
    /// <summary>
    /// 构建指定会话的统一运行态快照。
    /// </summary>
    Task<ChatSessionRuntimeSnapshot> InspectAsync(
        Guid userId,
        Guid sessionId,
        int queuedCount,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 会话统一运行态检查器实现。
/// </summary>
public sealed class ChatSessionRuntimeInspector : IChatSessionRuntimeInspector
{
    private readonly IPendingInteractionRepository _pendingInteractionRepository;
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly ICliRuntimeCoordinator _cliRuntimeCoordinator;

    public ChatSessionRuntimeInspector(
        IPendingInteractionRepository pendingInteractionRepository,
        IChatMessageRepository chatMessageRepository,
        ICliRuntimeCoordinator cliRuntimeCoordinator)
    {
        _pendingInteractionRepository = pendingInteractionRepository;
        _chatMessageRepository = chatMessageRepository;
        _cliRuntimeCoordinator = cliRuntimeCoordinator;
    }

    public async Task<ChatSessionRuntimeSnapshot> InspectAsync(
        Guid userId,
        Guid sessionId,
        int queuedCount,
        CancellationToken cancellationToken = default)
    {
        var pendingInteractions = await _pendingInteractionRepository.GetActiveBySessionIdAsync(sessionId, cancellationToken);
        var latestAssistantMessage = await _chatMessageRepository.GetLatestBySessionAndSenderAsync(
            sessionId,
            ChatConstants.RoleAssistant,
            cancellationToken);

        var cliSnapshot = await _cliRuntimeCoordinator.GetRuntimeSnapshotAsync(userId, sessionId, cancellationToken);

        return ChatSessionRuntimeResolver.Resolve(
            pendingInteractions,
            cliSnapshot,
            queuedCount,
            latestAssistantMessage);
    }
}
