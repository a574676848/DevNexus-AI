using DevNexus.Domain.Abstractions;
using DevNexus.Shared.Enums;

namespace DevNexus.ApiService.Services;

/// <summary>
/// 挂起交互过期治理后台服务。
/// 定期将过期的 PendingInteraction 收口为 Expired，并向前端推送最新状态。
/// </summary>
public sealed class PendingInteractionExpirationService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<PendingInteractionExpirationService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 构造函数。
    /// </summary>
    public PendingInteractionExpirationService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<PendingInteractionExpirationService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[PendingInteraction.Expiration] 过期治理任务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);
                await ExpirePendingInteractionsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PendingInteraction.Expiration] 过期治理任务异常");
            }
        }

        _logger.LogInformation("[PendingInteraction.Expiration] 过期治理任务已停止");
    }

    private async Task ExpirePendingInteractionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPendingInteractionRepository>();
        var runtimeEventNotifier = scope.ServiceProvider.GetRequiredService<IRuntimeEventNotifier>();

        var expiredInteractions = await repository.GetExpiredPendingAsync(DateTime.UtcNow, cancellationToken);
        if (expiredInteractions.Count == 0)
        {
            return;
        }

        foreach (var interaction in expiredInteractions)
        {
            interaction.Status = PendingInteractionStatus.Expired;
            interaction.UpdatedAt = DateTime.UtcNow;
            await repository.UpdateAsync(interaction, cancellationToken);
        }

        foreach (var sessionGroup in expiredInteractions.GroupBy(interaction => new
                 {
                     interaction.SessionId,
                     interaction.ChatSession.UserId
                 }))
        {
            foreach (var interaction in sessionGroup)
            {
                await runtimeEventNotifier.NotifyAsync(
                    sessionGroup.Key.UserId,
                    sessionGroup.Key.SessionId,
                    ServerEventType.PendingInteractionExpired,
                    new
                    {
                        InteractionId = interaction.Id,
                        Status = interaction.Status.ToWireValue(),
                        interaction.Title
                    },
                    cancellationToken);
            }
        }

        _logger.LogInformation(
            "[PendingInteraction.Expiration] 已处理过期挂起交互 | Count={Count} Sessions={Sessions}",
            expiredInteractions.Count,
            expiredInteractions.Select(item => item.SessionId).Distinct().Count());
    }
}
