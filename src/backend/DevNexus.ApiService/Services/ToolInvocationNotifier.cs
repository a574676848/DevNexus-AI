using DevNexus.ApiService.Hubs;
using DevNexus.Core.Services;
using DevNexus.Domain.Abstractions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using DevNexus.Shared.Utils;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevNexus.ApiService.Services;

/// <summary>
/// 工具调用状态通知服务
/// </summary>
public class ToolInvocationNotifier : IToolInvocationNotifier
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<ToolInvocationNotifier> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IRuntimeEventNotifier _runtimeEventNotifier;

    public ToolInvocationNotifier(
        IHubContext<ChatHub> hubContext,
        ILogger<ToolInvocationNotifier> logger,
        IServiceScopeFactory serviceScopeFactory,
        IRuntimeEventNotifier runtimeEventNotifier)
    {
        _hubContext = hubContext;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _runtimeEventNotifier = runtimeEventNotifier;
    }

    /// <inheritdoc />
    public async Task NotifyToolInvocationAsync(Guid userId, ToolInvocationDto invocation)
    {
        if (userId == Guid.Empty)
        {
            return;
        }

        try
        {
            var userGroup = $"user:{userId}";
            // 通过 ReceiveBlock + ThinkingBlockEmitter 推送状态，不再走旧 ToolInvocation 前端链路
            if (invocation.SessionId != Guid.Empty)
            {
                await _runtimeEventNotifier.NotifyAsync(
                    userId,
                    invocation.SessionId,
                    MapRuntimeEventType(invocation.Status),
                    new
                    {
                        invocation.ToolCallId,
                        invocation.PluginName,
                        invocation.FunctionName,
                        invocation.Status,
                        invocation.ErrorMessage,
                        invocation.DurationMs
                    });

                var thinkingText = BuildThinkingText(invocation);
                if (!string.IsNullOrWhiteSpace(thinkingText))
                {
                    // ★ 使用独立 scope 持久化到外部思维链临时字段
                    using var scope = _serviceScopeFactory.CreateScope();
                    var chatMessageRepository = scope.ServiceProvider.GetRequiredService<IChatMessageRepository>();
                    await ThinkingPersistenceHelper.PersistExternalThinkingAsync(
                        chatMessageRepository,
                        invocation.MessageId,
                        thinkingText,
                        _logger);

                    var block = ThinkingBlockEmitter.Create(
                        invocation.SessionId,
                        thinkingText,
                        invocation.MessageId,
                        new Dictionary<string, object>
                        {
                            [FeedbackBlockMetadataConstants.Source] = FeedbackBlockMetadataConstants.SourceToolInvocation,
                            [ToolBlockMetadataConstants.ToolCallId] = invocation.ToolCallId,
                            [FeedbackBlockMetadataConstants.ToolStatus] = FeedbackBlockMetadataConstants.NormalizeToolStatus(invocation.Status)
                        });

                    await _hubContext.Clients.Group(userGroup)
                        .SendAsync("ReceiveBlock", block);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[ToolInvocationNotifier] Failed to push tool invocation | UserId={UserId} ToolCallId={ToolCallId}",
                userId,
                invocation.ToolCallId);
        }
    }



    private static string BuildThinkingText(ToolInvocationDto invocation)
    {
        var fullName = $"{invocation.PluginName}.{invocation.FunctionName}";
        var status = ToolInvocationStatusExtensions.Parse(invocation.Status);

        return status switch
        {
            ToolInvocationStatus.Queued => $"🕒 工具 `{fullName}` 已排队，等待执行...",
            ToolInvocationStatus.Pending => $"🕒 工具 `{fullName}` 等待执行中...",
            ToolInvocationStatus.Running => $"🛠️ 正在调用工具 `{fullName}`...",

            ToolInvocationStatus.Completed => invocation.DurationMs.HasValue
                ? $"✅ 工具 `{fullName}` 执行完成 ({invocation.DurationMs.Value}ms)。"
                : $"✅ 工具 `{fullName}` 执行完成。",

            ToolInvocationStatus.Failed => string.IsNullOrWhiteSpace(invocation.ErrorMessage)
                ? $"❌ 工具 `{fullName}` 执行失败。"
                : $"❌ 工具 `{fullName}` 执行失败: {invocation.ErrorMessage}",
            ToolInvocationStatus.Cancelled => $"⏹️ 工具 `{fullName}` 已取消。",
            ToolInvocationStatus.Timeout => $"⌛ 工具 `{fullName}` 执行超时。",
            _ => string.Empty
        };
    }

    private static ServerEventType MapRuntimeEventType(string? status)
    {
        return ToolInvocationStatusExtensions.Parse(status) switch
        {
            ToolInvocationStatus.Completed => ServerEventType.ToolInvocationCompleted,
            ToolInvocationStatus.Failed or ToolInvocationStatus.Cancelled or ToolInvocationStatus.Timeout =>
                ServerEventType.ToolInvocationFailed,
            _ => ServerEventType.ToolInvocationStarted
        };
    }
}
