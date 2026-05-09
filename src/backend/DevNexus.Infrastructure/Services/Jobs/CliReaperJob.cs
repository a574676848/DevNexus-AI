using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DevNexus.Infrastructure.Services.CliTerminal;

namespace DevNexus.Infrastructure.Services.Jobs;

/// <summary>
/// CLI 会话死神清理任务
/// 每 3 分钟清理超过 10 分钟未活动的会话
/// </summary>
public class CliReaperJob : BackgroundService
{
    private readonly CliSessionManager _sessionManager;
    private readonly ILogger<CliReaperJob> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(3);
    private readonly TimeSpan _idleTimeout = TimeSpan.FromMinutes(10);
    private readonly TimeSpan _waitingForInputTimeout = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _maxRuntime = TimeSpan.FromMinutes(15);

    public CliReaperJob(CliSessionManager sessionManager, ILogger<CliReaperJob> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[CliReaper] 死神清理任务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);

                var cleanupResult = _sessionManager.CleanupExpiredSessions(
                    _idleTimeout,
                    _waitingForInputTimeout,
                    _maxRuntime);

                var cleanedCount = cleanupResult.IdleSessions + cleanupResult.WaitingSessions + cleanupResult.MaxRuntimeSessions;

                if (cleanedCount > 0)
                {
                    _logger.LogInformation(
                        "[CliReaper] 已清理 CLI 会话 | Total={Count} Idle={Idle} Waiting={Waiting} MaxRuntime={Runtime}",
                        cleanedCount,
                        cleanupResult.IdleSessions,
                        cleanupResult.WaitingSessions,
                        cleanupResult.MaxRuntimeSessions);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CliReaper] 清理任务异常");
            }
        }

        _logger.LogInformation("[CliReaper] 死神清理任务已停止");
    }
}
