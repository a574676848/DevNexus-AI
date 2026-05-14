using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using DevNexus.Core.Abstractions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using DevNexus.Infrastructure.Services.LLM;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Systems;

public class ConfirmationService : IConfirmationService
{
    private readonly ISwarmEventService _eventService;
    private readonly ILogger<ConfirmationService> _logger;
    private readonly ITokenAuditQueue _auditQueue;
    
    // 待处理的确认请求 ID -> TaskCompletionSource
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pendingConfirmations = new();

    public ConfirmationService(
        ISwarmEventService eventService,
        ILogger<ConfirmationService> logger,
        ITokenAuditQueue auditQueue)
    {
        _eventService = eventService;
        _logger = logger;
        _auditQueue = auditQueue;
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
            await QueueApprovalAuditAsync(sessionId, operationName, "Requested", null, cancellationToken);

            // 发送确认请求给前端
            await _eventService.NotifyConfirmationRequestedAsync(
                sessionId,
                confirmationId,
                operationName,
                payload,
                cancellationToken);

            // 等待用户响应或超时，超时自动拒绝。
            // 也可以配置 CancellationToken
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(AiOptimizationConstants.ApprovalTimeout);

            // 注册取消回调
            using (cts.Token.Register(() => tcs.TrySetCanceled()))
            {
                var approved = await tcs.Task;
                await QueueApprovalAuditAsync(
                    sessionId,
                    operationName,
                    approved ? "Approved" : "Rejected",
                    approved,
                    CancellationToken.None);
                return approved;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Confirmation {Id} timed out or cancelled.", confirmationId);
            await QueueApprovalAuditAsync(sessionId, operationName, "Timeout", false, CancellationToken.None);
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

    private async Task QueueApprovalAuditAsync(
        string sessionId,
        string operationName,
        string status,
        bool? approved,
        CancellationToken cancellationToken)
    {
        try
        {
            var ctx = TokenAuditContext.Current;
            var sessionGuid = Guid.TryParse(sessionId, out var parsedSessionId)
                ? parsedSessionId
                : ctx?.SessionId;
            var record = new ModelInvocationAuditRecord
            {
                OwnerType = ctx?.OwnerType ?? ModelInvocationOwnerTypes.System,
                OwnerUserId = ctx?.OwnerUserId,
                InvocationKind = ModelInvocationKinds.FunctionCall,
                SceneCode = ModelInvocationSceneCodes.ToolFunctionCall,
                SceneCategory = ctx?.SceneCategory ?? ModelInvocationSceneCategories.Other,
                ResourceType = ModelInvocationResourceTypes.Session,
                ResourceId = sessionGuid?.ToString(),
                SessionId = sessionGuid,
                MessageId = ctx?.MessageId,
                TraceId = ctx?.TraceId,
                ParentInvocationId = ctx?.ParentInvocationId,
                RootInvocationId = ctx?.RootInvocationId,
                ModelId = ctx?.ModelName ?? "approval",
                ProviderType = ModelInvocationProviderTypes.Llm,
                ProviderName = "approval",
                ProviderId = Guid.Empty.ToString(),
                MeteringType = ModelInvocationMeteringTypes.Request,
                MeteringValue = 1,
                UsageSource = ModelInvocationUsageSources.None,
                Status = status == "Approved" || status == "Requested"
                    ? ModelInvocationStatuses.Succeeded
                    : ModelInvocationStatuses.Failed,
                ErrorCode = approved == false ? "ApprovalRejected" : null,
                ErrorMessage = approved == false ? "用户拒绝或审批超时。" : null,
                ToolName = $"Approval.{operationName}",
                ToolArgumentsValid = true,
                ToolFailureReason = approved == false
                    ? ToolFailureReason.ApprovalRequired.ToWireValue()
                    : ToolFailureReason.None.ToWireValue(),
                ToolSuggestedAction = approved == null
                    ? ToolSuggestedAction.RequestApproval.ToWireValue()
                    : ToolSuggestedAction.None.ToWireValue(),
                ToolRetryable = false,
                ToolRequiresHumanIntervention = approved == null,
                ToolExitCode = 0
            };

            await _auditQueue.QueueBackgroundWorkItemAsync(record);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[Approval.Audit] 审批审计入队失败 | SessionId={SessionId} Operation={Operation}",
                sessionId,
                operationName);
        }
    }
}
