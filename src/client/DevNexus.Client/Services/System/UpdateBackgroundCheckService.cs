using DevNexus.Client.Shared.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DevNexus.Client.Services.System;

/// <summary>
/// 后台更新检查服务。
/// </summary>
public sealed class UpdateBackgroundCheckService : BackgroundService
{
    private readonly ILogger<UpdateBackgroundCheckService> _logger;
    private readonly IUpdateService _updateService;
    private readonly TimeSpan _initialDelay;
    private readonly TimeSpan _checkInterval;
    private int _isRunning;

    public UpdateBackgroundCheckService(
        ILogger<UpdateBackgroundCheckService> logger,
        IUpdateService updateService,
        IConfiguration configuration)
    {
        _logger = logger;
        _updateService = updateService;

        var intervalHours = configuration.GetValue<int>("Update:CheckIntervalHours", 24);
        _checkInterval = TimeSpan.FromHours(Math.Max(1, intervalHours));
        _initialDelay = TimeSpan.FromMinutes(5);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "[UpdateBackgroundCheckService] 启动更新检查服务 | 间隔={Interval}小时",
            _checkInterval.TotalHours);

        try
        {
            await _updateService.ResumePendingUpdateAsync();
            await Task.Delay(_initialDelay, stoppingToken);

            using var timer = new PeriodicTimer(_checkInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ExecuteCheckAsync();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[UpdateBackgroundCheckService] 更新检查服务已停止");
        }
    }

    private async Task ExecuteCheckAsync()
    {
        if (Interlocked.Exchange(ref _isRunning, 1) == 1)
        {
            _logger.LogWarning("[UpdateBackgroundCheckService] 上次检查仍在运行，跳过本次检查");
            return;
        }

        try
        {
            var update = await _updateService.CheckForUpdateAsync();
            if (update != null)
            {
                _logger.LogInformation(
                    "[UpdateBackgroundCheckService] 发现新版本 | Version={Version}",
                    update.Version);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UpdateBackgroundCheckService] 检查更新失败");
        }
        finally
        {
            Interlocked.Exchange(ref _isRunning, 0);
        }
    }
}
