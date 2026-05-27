using DevNexus.Core.Abstractions;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Logging;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 挂起交互过期收口协调器。
/// </summary>
public sealed class PendingInteractionExpirationCoordinator
{
    private readonly IPendingInteractionRepository _repository;
    private readonly IRuntimeEventNotifier _runtimeEventNotifier;
    private readonly IChatQueueDispatcher _chatQueueDispatcher;
    private readonly ILogger<PendingInteractionExpirationCoordinator> _logger;

    public PendingInteractionExpirationCoordinator(
        IPendingInteractionRepository repository,
        IRuntimeEventNotifier runtimeEventNotifier,
        IChatQueueDispatcher chatQueueDispatcher,
        ILogger<PendingInteractionExpirationCoordinator> logger)
    {
        _repository = repository;
        _runtimeEventNotifier = runtimeEventNotifier;
        _chatQueueDispatcher = chatQueueDispatcher;
        _logger = logger;
    }

    public async Task ExpireAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var expiredInteractions = await _repository.GetExpiredPendingAsync(utcNow, cancellationToken);
        if (expiredInteractions.Count == 0)
        {
            return;
        }

        foreach (var interaction in expiredInteractions)
        {
            interaction.Status = PendingInteractionStatus.Expired;
            interaction.UpdatedAt = utcNow;
            await _repository.UpdateAsync(interaction, cancellationToken);
        }

        foreach (var sessionGroup in expiredInteractions.GroupBy(CreateSessionKey))
        {
            foreach (var interaction in sessionGroup)
            {
                await NotifyExpiredAsync(sessionGroup.Key, interaction, cancellationToken);
            }

            await _chatQueueDispatcher.TriggerDispatchAsync(sessionGroup.Key.SessionId, cancellationToken);
        }

        _logger.LogInformation(
            "[PendingInteraction.Expiration] 已处理过期挂起交互 | Count={Count} Sessions={Sessions}",
            expiredInteractions.Count,
            expiredInteractions.Select(item => item.SessionId).Distinct().Count());
    }

    private static PendingInteractionSessionKey CreateSessionKey(PendingInteraction interaction)
        => new(interaction.SessionId, interaction.ChatSession.UserId);

    private Task NotifyExpiredAsync(
        PendingInteractionSessionKey sessionKey,
        PendingInteraction interaction,
        CancellationToken cancellationToken)
    {
        return _runtimeEventNotifier.NotifyAsync(
            sessionKey.UserId,
            sessionKey.SessionId,
            ServerEventType.PendingInteractionExpired,
            new
            {
                InteractionId = interaction.Id,
                Status = interaction.Status.ToWireValue(),
                interaction.Title
            },
            cancellationToken);
    }

    private readonly record struct PendingInteractionSessionKey(Guid SessionId, Guid UserId);
}
