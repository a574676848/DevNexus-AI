using DevNexus.ApiService.Hubs;
using DevNexus.Core.Services;
using DevNexus.Domain.Abstractions;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using DevNexus.Shared.Utils;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DevNexus.ApiService.Services;

/// <summary>
/// 客户端通知服务实现
/// </summary>
public class ClientNotifier : IClientNotifier
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IRuntimeEventNotifier _runtimeEventNotifier;
    private readonly ILogger<ClientNotifier> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    private static string GetUserGroupName(Guid userId) => $"user:{userId}";

    public ClientNotifier(
        IHubContext<ChatHub> hubContext,
        IRuntimeEventNotifier runtimeEventNotifier,
        ILogger<ClientNotifier> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _hubContext = hubContext;
        _runtimeEventNotifier = runtimeEventNotifier;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    /// <inheritdoc />
    public async Task NotifyMessageGeneratedAsync(Guid userId, Guid sessionId, ChatMessageDto message)
    {
        try
        {
            var userGroup = GetUserGroupName(userId);

            await _hubContext.Clients.Group(userGroup)
                .SendAsync("MessageReceived", message);

            _logger.LogInformation(
                "[ClientNotifier] Pushed message to user {UserId} session {SessionId}",
                userId,
                sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[ClientNotifier] Failed to push message to user {UserId} session {SessionId}",
                userId,
                sessionId);
        }
    }

    /// <inheritdoc />
    public async Task NotifyImageGenerationErrorAsync(Guid userId, Guid sessionId, string errorMessage)
    {
        try
        {
            await _runtimeEventNotifier.NotifyAsync(
                userId,
                sessionId,
                ServerEventType.GenerationFailed,
                new
                {
                    SessionId = sessionId,
                    ErrorMessage = errorMessage,
                    ErrorType = "ImageGenerationError"
                });

            _logger.LogInformation(
                "[ClientNotifier] 已推送图片生成失败运行时事件 | UserId={UserId} SessionId={SessionId}",
                userId,
                sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[ClientNotifier] 推送图片生成失败运行时事件失败 | UserId={UserId} SessionId={SessionId}",
                userId,
                sessionId);
        }
    }

    /// <inheritdoc />
    public async Task NotifyThinkingAsync(
        Guid userId,
        Guid sessionId,
        string content,
        Guid? messageId = null,
        Dictionary<string, object>? metadata = null)
    {
        if (userId == Guid.Empty || sessionId == Guid.Empty)
        {
            return;
        }

        try
        {
            if (messageId.HasValue && messageId.Value != Guid.Empty)
            {
                // ★ 使用独立 scope 持久化到外部思维链临时字段
                using var scope = _serviceScopeFactory.CreateScope();
                var chatMessageRepository = scope.ServiceProvider.GetRequiredService<IChatMessageRepository>();
                await ThinkingPersistenceHelper.PersistExternalThinkingAsync(
                    chatMessageRepository,
                    messageId.Value,
                    content,
                    _logger);
            }

            var userGroup = GetUserGroupName(userId);
            var block = ThinkingBlockEmitter.Create(sessionId, content, messageId, metadata);

            _logger.LogDebug(
                "[Thinking.Trace] StreamEmit | Source={Source} UserId={UserId} SessionId={SessionId} " +
                "MessageId={MessageId} Length={Length} Hash={Hash} Preview={Preview}",
                "ExternalNotifier",
                userId,
                sessionId,
                messageId,
                block.Content?.Length ?? 0,
                ComputeHash(block.Content),
                CreatePreview(block.Content));

            await _hubContext.Clients.Group(userGroup)
                .SendAsync("ReceiveBlock", block);

            _logger.LogDebug(
                "[ClientNotifier] Pushed thinking block | UserId={UserId} SessionId={SessionId}",
                userId,
                sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[ClientNotifier] Failed to push thinking block | UserId={UserId} SessionId={SessionId}",
                userId,
                sessionId);
        }
    }

    private static string ComputeHash(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return "empty";
        }

        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash[..6]);
    }

    private static string CreatePreview(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var normalized = Regex.Replace(content, @"\s+", " ").Trim();
        return normalized.Length <= 80 ? normalized : normalized[..80] + "...";
    }
}
