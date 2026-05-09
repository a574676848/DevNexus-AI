using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using DevNexus.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Systems;

public class ConfirmationService : IConfirmationService
{
    private readonly ISwarmEventService _eventService;
    private readonly ILogger<ConfirmationService> _logger;
    
    // 待处理的确认请求 ID -> TaskCompletionSource
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pendingConfirmations = new();

    public ConfirmationService(ISwarmEventService eventService, ILogger<ConfirmationService> logger)
    {
        _eventService = eventService;
        _logger = logger;
    }

    public async Task<bool> RequestConfirmationAsync(string sessionId, string operationName, string payload, CancellationToken cancellationToken = default)
    {
        var confirmationId = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        
        // 注册 TCS
        if (!_pendingConfirmations.TryAdd(confirmationId, tcs))
        {
            _logger.LogError("Failed to register confirmation request {Id}", confirmationId);
            return false; // 基本不可能发生
        }

        try
        {
            _logger.LogWarning("Blocking task for user confirmation: {Op} (Session: {Session})", operationName, sessionId);

            // 发送确认请求给前端
            await _eventService.NotifyConfirmationRequestedAsync(
                sessionId,
                confirmationId,
                operationName,
                payload,
                cancellationToken);

            // 等待用户响应或超时 (默认 5 分钟超时自动拒绝)
            // 也可以配置 CancellationToken
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMinutes(5));

            // 注册取消回调
            using (cts.Token.Register(() => tcs.TrySetCanceled()))
            {
                return await tcs.Task;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Confirmation {Id} timed out or cancelled.", confirmationId);
            return false;
        }
        finally
        {
            _pendingConfirmations.TryRemove(confirmationId, out _);
        }
    }

    public void ResolveConfirmation(string confirmationId, bool approved)
    {
        if (_pendingConfirmations.TryGetValue(confirmationId, out var tcs))
        {
            _logger.LogInformation("Resolving confirmation {Id}: {Result}", confirmationId, approved ? "APPROVED" : "REJECTED");
            tcs.TrySetResult(approved);
        }
        else
        {
            _logger.LogWarning("Attempted to resolve unknown confirmation {Id}", confirmationId);
        }
    }
}
