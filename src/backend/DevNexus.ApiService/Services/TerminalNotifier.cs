using DevNexus.ApiService.Hubs;
using DevNexus.Core.Abstractions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DevNexus.ApiService.Services;

/// <summary>
/// 终端实时通知服务实现 (Phase 8: 纯缓冲模式)
/// </summary>
public class TerminalNotifier : ITerminalNotifier
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<TerminalNotifier> _logger;
    private readonly ITerminalOutputBuffer _terminalOutputBuffer;
    private readonly IRuntimeEventNotifier _runtimeEventNotifier;

    public TerminalNotifier(
        IHubContext<ChatHub> hubContext,
        ILogger<TerminalNotifier> logger,
        ITerminalOutputBuffer terminalOutputBuffer,
        IRuntimeEventNotifier runtimeEventNotifier)
    {
        _hubContext = hubContext;
        _logger = logger;
        _terminalOutputBuffer = terminalOutputBuffer;
        _runtimeEventNotifier = runtimeEventNotifier;
    }

    /// <inheritdoc />
    public async Task NotifyTerminalOutputAsync(
        Guid userId,
        Guid sessionId,
        Guid messageId,
        string outputDelta,
        bool isLast = false,
        Dictionary<string, object>? metadata = null)
    {
        if (userId == Guid.Empty || (string.IsNullOrEmpty(outputDelta) && !isLast))
        {
            return;
        }

        try
        {
            var userGroup = $"user:{userId}";
            var effectiveMetadata = metadata != null
                ? new Dictionary<string, object>(metadata)
                : new Dictionary<string, object>();
            var streamId = GetGuidFromMetadata(effectiveMetadata, TerminalBlockMetadataKeys.TerminalStreamId);

            // 终端结束前先落库并回填归档/观察摘要，确保最终事件与最终块使用统一事实。
            TerminalOutputContentDto? archivedOutput = null;
            if (streamId.HasValue && streamId.Value != Guid.Empty)
            {
                if (!string.IsNullOrEmpty(outputDelta))
                {
                    await _terminalOutputBuffer.AppendAsync(streamId.Value, outputDelta, effectiveMetadata);
                }

                if (isLast)
                {
                    var flushed = await _terminalOutputBuffer.FlushAsync(streamId.Value);
                    if (flushed)
                    {
                        archivedOutput = await _terminalOutputBuffer.ReadOutputAsync(streamId.Value);
                        EnrichTerminalMetadata(effectiveMetadata, archivedOutput);
                        _terminalOutputBuffer.Remove(streamId.Value);

                        _logger.LogDebug(
                            "[Terminal.Notifier] Terminal stream flushed and removed | StreamId={StreamId} MessageId={MessageId}",
                            streamId.Value, messageId);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "[Terminal.Notifier] Failed to flush terminal stream | StreamId={StreamId} MessageId={MessageId}",
                            streamId, messageId);
                    }
                }
            }

            var block = new BlockDto
            {
                BlockId = Guid.NewGuid(),
                SessionId = sessionId,
                MessageId = messageId,
                BlockType = BlockType.Terminal,
                Content = outputDelta,
                IsLast = isLast,
                Metadata = effectiveMetadata
            };

            // 实时推送到前端
            await _hubContext.Clients.Group(userGroup).SendAsync("ReceiveBlock", block);

            var cliSessionState = CliExecutionRuntimeMapper.CreateDto(sessionId, effectiveMetadata);
            if (cliSessionState != null)
            {
                var eventPayload = BuildRuntimeEventPayload(
                    cliSessionState,
                    BuildTerminalCompletionMessage(cliSessionState.ExecStatus, archivedOutput?.WatchSummary),
                    archivedOutput);

                await _runtimeEventNotifier.NotifyAsync(
                    userId,
                    sessionId,
                    CliExecutionRuntimeMapper.ResolveEventType(cliSessionState, preferOutputEvent: true),
                    eventPayload);
            }
            else if (messageId != Guid.Empty && metadata != null && (!streamId.HasValue || streamId.Value == Guid.Empty))
            {
                _logger.LogWarning(
                    "[Terminal.Notifier] No terminalStreamId provided in metadata | MessageId={MessageId}",
                    messageId);
            }

            if (isLast)
            {
                _logger.LogDebug(
                    "[Terminal.Notifier] Terminal session complete pushed | SessionId={SessionId} MessageId={MessageId}",
                    sessionId, messageId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[Terminal.Notifier] Failed to push terminal output | UserId={UserId} SessionId={SessionId}",
                userId, sessionId);
        }
    }

    private static object BuildRuntimeEventPayload(
        CliSessionStateDto cliSessionState,
        string? message,
        TerminalOutputContentDto? archivedOutput)
    {
        return new
        {
            State = cliSessionState,
            Message = message,
            WatchSummary = archivedOutput?.WatchSummary,
            HasArchivedOutput = archivedOutput?.HasArchivedOutput ?? false,
            OutputLength = archivedOutput?.OutputLength ?? 0,
            OutputLineCount = archivedOutput?.OutputLineCount ?? 0,
            QuietSuccess = IsQuietSuccess(cliSessionState.ExecStatus, archivedOutput)
        };
    }

    private static void EnrichTerminalMetadata(
        Dictionary<string, object> metadata,
        TerminalOutputContentDto? archivedOutput)
    {
        if (archivedOutput == null)
        {
            return;
        }

        metadata[TerminalBlockMetadataKeys.HasArchivedOutput] = archivedOutput.HasArchivedOutput;
        metadata[TerminalBlockMetadataKeys.OutputLength] = archivedOutput.OutputLength;
        metadata[TerminalBlockMetadataKeys.OutputLineCount] = archivedOutput.OutputLineCount;

        if (!string.IsNullOrWhiteSpace(archivedOutput.WatchSummary))
        {
            metadata[TerminalBlockMetadataKeys.WatchSummary] = archivedOutput.WatchSummary;
        }
    }

    private static string? BuildTerminalCompletionMessage(CliExecStatus status, string? watchSummary)
    {
        var baseMessage = status switch
        {
            CliExecStatus.Completed => "终端执行完成。",
            CliExecStatus.RolledBack => "终端执行已回滚。",
            CliExecStatus.Failed => "终端执行失败。",
            CliExecStatus.TimedOut => "终端执行超时。",
            CliExecStatus.Cancelled => "终端执行已取消。",
            CliExecStatus.Reaped => "终端执行已被运行时回收。",
            _ => null
        };

        if (string.IsNullOrWhiteSpace(baseMessage))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(watchSummary)
            ? baseMessage
            : $"{baseMessage} {watchSummary}";
    }

    private static bool IsQuietSuccess(CliExecStatus status, TerminalOutputContentDto? archivedOutput)
    {
        if (status != CliExecStatus.Completed || archivedOutput == null)
        {
            return false;
        }

        return archivedOutput.OutputLength <= 0
            && string.IsNullOrWhiteSpace(archivedOutput.WatchSummary);
    }

    /// <summary>
    /// 从 metadata 中提取 Guid 值
    /// </summary>
    private static Guid? GetGuidFromMetadata(Dictionary<string, object>? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is Guid guidValue)
        {
            return guidValue;
        }

        if (value is string strValue && Guid.TryParse(strValue, out var parsed))
        {
            return parsed;
        }

        // 处理 JsonElement 类型
        if (value is System.Text.Json.JsonElement element && element.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            if (Guid.TryParse(element.GetString(), out var parsedFromJson))
            {
                return parsedFromJson;
            }
        }

        return null;
    }
    private static string? GetStringFromMetadata(Dictionary<string, object>? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is System.Text.Json.JsonElement element)
        {
            return element.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => element.GetString(),
                System.Text.Json.JsonValueKind.Number => element.ToString(),
                System.Text.Json.JsonValueKind.True => bool.TrueString,
                System.Text.Json.JsonValueKind.False => bool.FalseString,
                _ => null
            };
        }

        return value.ToString();
    }

    private static bool? GetBoolFromMetadata(Dictionary<string, object>? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is bool boolValue)
        {
            return boolValue;
        }

        if (value is System.Text.Json.JsonElement element)
        {
            return element.ValueKind switch
            {
                System.Text.Json.JsonValueKind.True => true,
                System.Text.Json.JsonValueKind.False => false,
                System.Text.Json.JsonValueKind.String when bool.TryParse(element.GetString(), out var parsed) => parsed,
                _ => null
            };
        }

        return bool.TryParse(value.ToString(), out var parsedValue) ? parsedValue : null;
    }

    private static DateTime? GetDateTimeFromMetadata(Dictionary<string, object>? metadata, string key)
    {
        var value = GetStringFromMetadata(metadata, key);
        return DateTime.TryParse(value, out var parsed) ? parsed : null;
    }
}
