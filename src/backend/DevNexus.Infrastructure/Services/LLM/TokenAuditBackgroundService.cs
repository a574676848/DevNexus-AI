using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DevNexus.Domain.Abstractions; 
using DevNexus.Shared.DTOs;
using DevNexus.Infrastructure.Services.LLM;

namespace DevNexus.Infrastructure.Services.LLM;

public class TokenAuditBackgroundService : BackgroundService
{
    private readonly ITokenAuditQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TokenAuditBackgroundService> _logger;

    public TokenAuditBackgroundService(
        ITokenAuditQueue queue,
        IServiceProvider serviceProvider,
        ILogger<TokenAuditBackgroundService> logger)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[TokenAudit] Background service is running.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var workItem = await _queue.DequeueAsync(stoppingToken);

                await ProcessWorkItemAsync(workItem);
            }
            catch (OperationCanceledException)
            {
                // 正常关闭
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TokenAudit] Error occurred executing background work item.");
            }
        }

        _logger.LogInformation("[TokenAudit] Background service is stopping.");
    }

    private async Task ProcessWorkItemAsync(ModelInvocationAuditRecord record)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var auditAnalyticsService = scope.ServiceProvider.GetRequiredService<IAuditAnalyticsWriteService>();

            await auditAnalyticsService.RecordUsageAsync(record);

            _logger.LogDebug(
                "[TokenAudit] Persisted audit record | SceneCode={SceneCode} SessionId={SessionId} MessageId={MessageId}",
                record.SceneCode,
                record.SessionId,
                record.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[TokenAudit] Failed to persist model invocation audit | SceneCode={SceneCode} SessionId={SessionId} MessageId={MessageId}",
                record.SceneCode,
                record.SessionId,
                record.MessageId);
        }
    }
}
