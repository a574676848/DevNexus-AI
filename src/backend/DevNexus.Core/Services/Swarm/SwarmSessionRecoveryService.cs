using DevNexus.Domain.Abstractions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DevNexus.Core.Services.Swarm;

/// <summary>
/// 应用启动时处理异常中断的 Swarm 会话。
/// </summary>
/// <remarks>
/// 在新的上下文驱动主链下，启动恢复不再尝试继续执行旧的运行中任务，
/// 而是将异常中断的会话统一标记为失败，避免进程重启后进入不一致状态。
/// </remarks>
public class SwarmSessionRecoveryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SwarmSessionRecoveryService> _logger;

    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 初始化恢复服务。
    /// </summary>
    public SwarmSessionRecoveryService(
        IServiceScopeFactory scopeFactory,
        ILogger<SwarmSessionRecoveryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(StartupDelay, stoppingToken);

        _logger.LogInformation("[Swarm 恢复] 开始扫描异常中断的会话。");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sessionRepository = scope.ServiceProvider.GetRequiredService<IContextSwarmSessionRepository>();
            var chatMessageRepository = scope.ServiceProvider.GetRequiredService<IChatMessageRepository>();
            var swarmEventService = scope.ServiceProvider.GetRequiredService<Core.Abstractions.ISwarmEventService>();

            var interruptedSessions = await sessionRepository.GetInterruptedSessionsAsync();
            if (interruptedSessions.Count == 0)
            {
                _logger.LogInformation("[Swarm 恢复] 未发现异常中断会话。");
                return;
            }

            _logger.LogWarning("[Swarm 恢复] 发现 {Count} 个异常中断会话，将统一标记为失败。", interruptedSessions.Count);

            foreach (var session in interruptedSessions)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                await MarkSessionAsFailedAsync(
                    sessionRepository,
                    chatMessageRepository,
                    swarmEventService,
                    session,
                    stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("[Swarm 恢复] 服务停止，结束会话扫描。");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Swarm 恢复] 扫描异常中断会话时发生错误。");
        }
    }

    /// <summary>
    /// 将异常中断会话标记为失败并同步消息状态。
    /// </summary>
    private async Task MarkSessionAsFailedAsync(
        IContextSwarmSessionRepository sessionRepository,
        IChatMessageRepository chatMessageRepository,
        Core.Abstractions.ISwarmEventService swarmEventService,
        Domain.Entities.ContextSwarmSession session,
        CancellationToken cancellationToken)
    {
        var failureReason = "Swarm 会话在服务重启前异常中断，已终止本次执行。";
        await sessionRepository.UpdateSessionStatusAsync(session.SessionId, SwarmStatus.Failed, failureReason);

        if (Guid.TryParse(session.SessionId, out var chatSessionId))
        {
            var lastAiMessage = await chatMessageRepository.GetLatestBySessionAndSenderAsync(
                chatSessionId,
                ChatConstants.RoleAssistant,
                cancellationToken);

            if (lastAiMessage != null)
            {
                lastAiMessage.Content = new Dictionary<string, object>
                {
                    ["text"] = failureReason
                };
                lastAiMessage.Status = ChatConstants.StatusCancelled;
                lastAiMessage.UpdatedAt = DateTime.UtcNow;
                await chatMessageRepository.UpdateAsync(lastAiMessage, cancellationToken);
            }
        }

        await swarmEventService.NotifySwarmFailedAsync(
            session.SessionId,
            failureReason,
            cancellationToken);

        _logger.LogWarning("[Swarm 恢复] 会话已标记失败 | SessionId={SessionId}", session.SessionId);
    }
}
