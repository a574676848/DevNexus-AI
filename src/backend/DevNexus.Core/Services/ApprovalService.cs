// using DevNexus.Domain.Abstractions via GlobalUsings
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DevNexus.Core.Services;

/// <summary>
/// 人机回环审批服务实现
/// 使用 TaskCompletionSource 管理异步审批等待
/// </summary>
public class ApprovalService : IApprovalService
{
    private readonly ILogger<ApprovalService> _logger;
    
    /// <summary>
    /// 待审批操作的等待源
    /// Key: ActionId, Value: TaskCompletionSource
    /// </summary>
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ApprovalResponse>> _pendingApprovals = new();
    
    /// <summary>
    /// 待审批操作的通知信息
    /// Key: ActionId, Value: PendingApprovalNotification
    /// </summary>
    private readonly ConcurrentDictionary<Guid, PendingApprovalNotification> _pendingNotifications = new();
    
    /// <summary>
    /// 会话到操作的映射（用于快速查找）
    /// Key: SessionId, Value: ActionId
    /// </summary>
    private readonly ConcurrentDictionary<Guid, Guid> _sessionToAction = new();
    
    /// <summary>
    /// 默认审批超时时间（秒）
    /// </summary>
    private const int DefaultTimeoutSeconds = 300;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public ApprovalService(ILogger<ApprovalService> logger)
    {
        _logger = logger;
    }
    
    /// <inheritdoc />
    public async Task<ApprovalResponse> RequestApprovalAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[Approval.Request] Requesting approval | ActionId={ActionId} ActionType={ActionType} SessionId={SessionId}",
            request.ActionId,
            request.ActionType,
            request.SessionId);
        
        // 创建等待源
        var tcs = new TaskCompletionSource<ApprovalResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        
        // 注册到待审批列表
        if (!_pendingApprovals.TryAdd(request.ActionId, tcs))
        {
            throw new InvalidOperationException($"Approval request {request.ActionId} already exists");
        }
        
        // 创建通知信息
        var notification = new PendingApprovalNotification
        {
            ActionId = request.ActionId,
            ActionType = request.ActionType,
            SessionId = request.SessionId,
            Description = request.Description,
            Payload = request.Payload,
            TimeoutSeconds = DefaultTimeoutSeconds,
            CreatedAt = DateTime.UtcNow
        };
        
        _pendingNotifications.TryAdd(request.ActionId, notification);
        _sessionToAction.TryAdd(request.SessionId, request.ActionId);
        
        try
        {
            // 创建超时取消令牌
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(DefaultTimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            
            // 注册取消回调
            linkedCts.Token.Register(() =>
            {
                if (_pendingApprovals.TryRemove(request.ActionId, out var pendingTcs))
                {
                    var response = new ApprovalResponse
                    {
                        ActionId = request.ActionId,
                        Approved = false,
                        RejectionReason = cancellationToken.IsCancellationRequested 
                            ? "操作已取消" 
                            : "审批超时",
                        RespondedAt = DateTime.UtcNow
                    };
                    pendingTcs.TrySetResult(response);
                }
            });
            
            // 等待审批结果
            var result = await tcs.Task;
            
            _logger.LogInformation(
                "[Approval.Response] Approval completed | ActionId={ActionId} Approved={Approved}",
                request.ActionId,
                result.Approved);
            
            return result;
        }
        finally
        {
            // 清理
            _pendingApprovals.TryRemove(request.ActionId, out _);
            _pendingNotifications.TryRemove(request.ActionId, out _);
            _sessionToAction.TryRemove(request.SessionId, out _);
        }
    }
    
    /// <inheritdoc />
    public Task ApproveAsync(Guid actionId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[Approval.Approve] User approved action | ActionId={ActionId}",
            actionId);
        
        if (_pendingApprovals.TryGetValue(actionId, out var tcs))
        {
            var response = new ApprovalResponse
            {
                ActionId = actionId,
                Approved = true,
                RespondedAt = DateTime.UtcNow
            };
            tcs.TrySetResult(response);
        }
        else
        {
            _logger.LogWarning(
                "[Approval.Approve] Approval not found | ActionId={ActionId}",
                actionId);
        }
        
        return Task.CompletedTask;
    }
    
    /// <inheritdoc />
    public Task RejectAsync(Guid actionId, string? reason = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[Approval.Reject] User rejected action | ActionId={ActionId} Reason={Reason}",
            actionId,
            reason);
        
        if (_pendingApprovals.TryGetValue(actionId, out var tcs))
        {
            var response = new ApprovalResponse
            {
                ActionId = actionId,
                Approved = false,
                RejectionReason = reason ?? "用户拒绝",
                RespondedAt = DateTime.UtcNow
            };
            tcs.TrySetResult(response);
        }
        else
        {
            _logger.LogWarning(
                "[Approval.Reject] Approval not found | ActionId={ActionId}",
                actionId);
        }
        
        return Task.CompletedTask;
    }
    
    /// <inheritdoc />
    public PendingApprovalNotification? GetPendingApproval(Guid sessionId)
    {
        if (_sessionToAction.TryGetValue(sessionId, out var actionId))
        {
            if (_pendingNotifications.TryGetValue(actionId, out var notification))
            {
                return notification;
            }
        }
        return null;
    }
    
    /// <inheritdoc />
    public PendingApprovalNotification? GetPendingApprovalById(Guid actionId)
    {
        _pendingNotifications.TryGetValue(actionId, out var notification);
        return notification;
    }
    
    /// <inheritdoc />
    public void CancelPendingApproval(Guid actionId)
    {
        _logger.LogInformation(
            "[Approval.Cancel] Cancelling pending approval | ActionId={ActionId}",
            actionId);
        
        if (_pendingApprovals.TryRemove(actionId, out var tcs))
        {
            var response = new ApprovalResponse
            {
                ActionId = actionId,
                Approved = false,
                RejectionReason = "操作已取消",
                RespondedAt = DateTime.UtcNow
            };
            tcs.TrySetResult(response);
        }
        
        _pendingNotifications.TryRemove(actionId, out _);
    }
}
