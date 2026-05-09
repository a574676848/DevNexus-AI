using DevNexus.Client.Shared.Models;
using DevNexus.Client.Shared.Services.UI;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using System.Text.Json;

namespace DevNexus.Client.Shared.Components.Chat;

/// <summary>
/// ChatContainer 运行时同步辅助逻辑。
/// 负责会话恢复、挂起交互恢复与 SignalR 运行态同步。
/// </summary>
public partial class ChatContainer
{
    private string GetChatContextSummary() => string.Empty;

    private IReadOnlyList<string> GetChatContextHighlights() => Array.Empty<string>();

    private Task ToggleTerminalPanelAsync()
    {
        if (ChatState.CurrentSessionId != Guid.Empty)
        {
            ChatState.OpenChatTerminalSidekick(ChatState.CurrentSessionId);
        }

        return Task.CompletedTask;
    }

    private Task HandleDevModeChangedAsync(bool _)
    {
        return Task.CompletedTask;
    }

    private static string FormatCompactId(Guid? id)
    {
        return id.HasValue && id.Value != Guid.Empty
            ? id.Value.ToString("N")[..8]
            : "unknown";
    }

    private async Task RestoreCliExecSessionAsync(Guid sessionId, IReadOnlyList<ChatMessageDto> _messages)
    {
        try
        {
            var state = await SignalR.GetCliExecSessionAsync(sessionId);
            if (state != null)
            {
                ChatState.UpdateCliExecSession(state);
            }
        }
        catch
        {
            // 恢复失败不阻断会话加载
        }
    }

    private async Task RestorePendingInteractionsAsync(Guid sessionId)
    {
        try
        {
            var interactions = await ApiService.GetPendingInteractionsAsync(sessionId);
            ChatState.SetPendingInteractions(sessionId, interactions);
        }
        catch (Exception ex)
        {
            await RemoteLogService.LogErrorAsync(ex, "ChatContainer.RestorePendingInteractionsAsync", new Dictionary<string, object?>
            {
                ["SessionId"] = sessionId
            });
        }
    }

    private async Task RestoreSessionRuntimeAsync(Guid sessionId)
    {
        if (sessionId == Guid.Empty)
        {
            return;
        }

        try
        {
            var runtime = await ApiService.GetSessionRuntimeAsync(sessionId);
            ChatState.SetSessionRuntime(sessionId, runtime);
        }
        catch (Exception ex)
        {
            await RemoteLogService.LogErrorAsync(ex, "ChatContainer.RestoreSessionRuntimeAsync", new Dictionary<string, object?>
            {
                ["SessionId"] = sessionId
            });
        }
    }

    private async Task RefreshRuntimeSnapshotAsync(
        Guid sessionId,
        bool refreshCliExecSession = false,
        bool refreshPendingInteractions = false)
    {
        if (sessionId == Guid.Empty)
        {
            return;
        }

        if (refreshCliExecSession)
        {
            await RestoreCliExecSessionAsync(sessionId, _messages);
        }

        await RestoreSessionRuntimeAsync(sessionId);

        var runtime = ChatState.GetSessionRuntime(sessionId);
        if (runtime == null)
        {
            return;
        }

        if (runtime.PendingInteractionCount <= 0)
        {
            ChatState.ClearPendingInteractions(sessionId);
            return;
        }

        if (CanRenderPendingInteractionFromRuntime(runtime, sessionId))
        {
            return;
        }

        var shouldRefreshPendingDetails = refreshPendingInteractions
            || ChatState.GetPendingInteractions(sessionId).Count != runtime.PendingInteractionCount;

        if (shouldRefreshPendingDetails)
        {
            await RestorePendingInteractionsAsync(sessionId);
        }
    }

    private void ScheduleRuntimeRefresh(Guid sessionId)
    {
        if (sessionId == Guid.Empty)
        {
            return;
        }

        _ = InvokeAsync(async () =>
        {
            await RefreshRuntimeSnapshotAsync(sessionId);
            StateHasChanged();
        });
    }

    private bool CanRenderPendingInteractionFromRuntime(ChatSessionRuntimeDto runtime, Guid sessionId)
    {
        if (runtime.PendingInteractionCount != 1
            || runtime.PrimaryPendingInteractionId is null
            || runtime.PrimaryPendingInteractionKind != PendingInteractionKind.Approval)
        {
            return false;
        }

        return ChatState.GetPendingInteractions(sessionId).Count == 0;
    }

    private async Task HandlePendingInteractionResolveAsync(PendingInteractionResolveSubmission submission)
    {
        if (ChatState.CurrentSessionId == Guid.Empty)
        {
            return;
        }

        var response = await ApiService.ResolvePendingInteractionAsync(
            ChatState.CurrentSessionId,
            submission.InteractionId,
            new PendingInteractionResolutionRequest
            {
                Action = submission.Action,
                Values = submission.Values
            });

        await RefreshRuntimeSnapshotAsync(
            ChatState.CurrentSessionId,
            refreshPendingInteractions: true);

        if (!response.ShouldResume || string.IsNullOrWhiteSpace(response.ResumeMessage))
        {
            return;
        }

        await HandleSendWithProviderAsync(new ChatComposerSubmission
        {
            Content = response.ResumeMessage,
            EnableRag = true,
            Metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                [ChatMessageMetadataKeys.PendingInteractionId] = response.InteractionId,
                [ChatMessageMetadataKeys.ResumePendingInteraction] = true
            }
        });
    }

    private Task PromoteOptimisticUserMessageAsync(Guid _)
    {
        return Task.CompletedTask;
    }

    private void SyncBlocks(List<BlockDto>? target, IReadOnlyList<BlockDto>? source)
    {
        if (target == null || source == null)
        {
            return;
        }

        target.Clear();
        target.AddRange(source);
    }

    private void SyncArtifacts(List<ArtifactDto>? target, IReadOnlyList<ArtifactDto>? source)
    {
        if (target == null || source == null)
        {
            return;
        }

        target.Clear();
        target.AddRange(source);
    }

    private void HandleBlockReceived(BlockDto block)
    {
        if (block.SessionId != ChatState.CurrentSessionId)
        {
            return;
        }

        lock (_pendingBlockSync)
        {
            _pendingBlocks.Add(block);
            if (_hasPendingBlockFlush)
            {
                return;
            }

            _hasPendingBlockFlush = true;
        }

        _throttledRenderAction?.Invoke();
    }

    private void HandleMessageReceived(ChatMessageDto message)
    {
        if (message.ChatSessionId != ChatState.CurrentSessionId)
        {
            return;
        }

        var index = _messages.FindIndex(item => item.Id == message.Id);
        if (index >= 0)
        {
            _messages[index] = message;
        }
        else
        {
            _messages.Add(message);
        }

        _lastStreamingActivityAt = DateTime.UtcNow;
        _ = InvokeAsync(StateHasChanged);
    }

    private void HandleConnectionChanged(bool connected)
    {
        ChatState.SetRealtimeConnectionState(connected);

        if (connected && ChatState.CurrentSessionId != Guid.Empty)
        {
            ScheduleRuntimeRefresh(ChatState.CurrentSessionId);
            return;
        }

        _ = InvokeAsync(StateHasChanged);
    }

    private void HandleServerEvent(ServerEvent serverEvent)
    {
        if (serverEvent.SessionId == Guid.Empty)
        {
            return;
        }

        switch (serverEvent.EventType)
        {
            case ServerEventType.GenerationStarted:
                ChatState.SetSessionGeneratingOptimistic(serverEvent.SessionId, true);
                ScheduleRuntimeRefresh(serverEvent.SessionId);
                return;
            case ServerEventType.GenerationCompleted:
            case ServerEventType.GenerationCancelled:
                ChatState.SetSessionGeneratingOptimistic(serverEvent.SessionId, false);
                _generationTimeoutNotified = false;
                ScheduleRuntimeRefresh(serverEvent.SessionId);
                return;
            case ServerEventType.GenerationFailed:
                ChatState.SetSessionGeneratingOptimistic(serverEvent.SessionId, false);
                if (TryGetStringProperty(serverEvent.Data, "ErrorMessage", out var errorMessage)
                    && !string.IsNullOrWhiteSpace(errorMessage))
                {
                    _ = NotificationService.ShowDeduplicatedAsync(
                        "生成失败",
                        errorMessage,
                        suppressSeconds: 8,
                        dedupeKey: $"runtime-generation-failed:{serverEvent.SessionId}");
                }
                ScheduleRuntimeRefresh(serverEvent.SessionId);
                return;
            case ServerEventType.CliExecRequested:
            case ServerEventType.CliExecStarted:
            case ServerEventType.CliExecOutputUpdated:
            case ServerEventType.CliExecWaitingForInput:
                _ = InvokeAsync(async () =>
                {
                    await RefreshRuntimeSnapshotAsync(
                        serverEvent.SessionId,
                        refreshCliExecSession: true);
                    StateHasChanged();
                });
                return;
            case ServerEventType.CliExecCompleted:
                _ = InvokeAsync(async () =>
                {
                    await RefreshRuntimeSnapshotAsync(
                        serverEvent.SessionId,
                        refreshCliExecSession: true);
                    ShowCliExecTerminalToast(serverEvent, ToastType.Success);
                    StateHasChanged();
                });
                return;
            case ServerEventType.CliExecFailed:
            case ServerEventType.CliExecCancelled:
            case ServerEventType.CliExecTimedOut:
                _ = InvokeAsync(async () =>
                {
                    await RefreshRuntimeSnapshotAsync(
                        serverEvent.SessionId,
                        refreshCliExecSession: true);
                    ShowCliExecTerminalToast(serverEvent, ToastType.Warning);
                    StateHasChanged();
                });
                return;
            case ServerEventType.CliExecRolledBack:
                _ = InvokeAsync(async () =>
                {
                    await RefreshRuntimeSnapshotAsync(
                        serverEvent.SessionId,
                        refreshCliExecSession: true);

                    if (TryGetStringProperty(serverEvent.Data, "Message", out var rollbackMessage)
                        && !string.IsNullOrWhiteSpace(rollbackMessage))
                    {
                        ToastService?.Show(rollbackMessage, ToastType.Success, 4000);
                    }

                    StateHasChanged();
                });
                return;
            case ServerEventType.CliExecApprovalRequired:
                _ = InvokeAsync(async () =>
                {
                    await RefreshRuntimeSnapshotAsync(
                        serverEvent.SessionId,
                        refreshPendingInteractions: true,
                        refreshCliExecSession: true);

                    if (TryGetStringProperty(serverEvent.Data, "Message", out var approvalMessage)
                        && !string.IsNullOrWhiteSpace(approvalMessage))
                    {
                        ToastService?.Show(approvalMessage, ToastType.Warning, 4000);
                    }

                    StateHasChanged();
                });
                return;
            case ServerEventType.CliExecRejected:
                _ = InvokeAsync(async () =>
                {
                    await RefreshRuntimeSnapshotAsync(serverEvent.SessionId);

                    if (TryGetStringProperty(serverEvent.Data, "Message", out var rejectedMessage)
                        && !string.IsNullOrWhiteSpace(rejectedMessage))
                    {
                        ToastService?.Show(rejectedMessage, ToastType.Warning, 4000);
                    }

                    StateHasChanged();
                });
                return;
            case ServerEventType.QueueStateChanged:
                _ = InvokeAsync(async () =>
                {
                    await RestoreQueuedMessagesAsync(serverEvent.SessionId);
                    await RefreshRuntimeSnapshotAsync(serverEvent.SessionId);

                    if (TryGetStringProperty(serverEvent.Data, "Message", out var queueMessage)
                        && !string.IsNullOrWhiteSpace(queueMessage))
                    {
                        ToastService?.Show(queueMessage, ToastType.Info, 3000);
                    }

                    StateHasChanged();
                });
                return;
            case ServerEventType.ToolInvocationStarted:
            case ServerEventType.ToolInvocationCompleted:
            case ServerEventType.ToolInvocationFailed:
            case ServerEventType.PendingInteractionCreated:
            case ServerEventType.PendingInteractionResolved:
            case ServerEventType.PendingInteractionExpired:
            case ServerEventType.SessionSuspended:
            case ServerEventType.SessionResumed:
            case ServerEventType.SessionCancelled:
                _ = InvokeAsync(async () =>
                {
                    if (serverEvent.EventType is ServerEventType.PendingInteractionCreated
                        or ServerEventType.PendingInteractionResolved
                        or ServerEventType.PendingInteractionExpired)
                    {
                        await RefreshRuntimeSnapshotAsync(
                            serverEvent.SessionId,
                            refreshPendingInteractions: true);
                    }
                    else
                    {
                        await RefreshRuntimeSnapshotAsync(serverEvent.SessionId);
                    }
                    StateHasChanged();
                });
                return;
        }
    }

    private static bool TryGetStringProperty(object? data, string propertyName, out string? value)
    {
        value = null;

        if (data is not JsonElement element || element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        value = property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
        return true;
    }

    private void ShowCliExecTerminalToast(ServerEvent serverEvent, ToastType fallbackType)
    {
        if (fallbackType == ToastType.Success
            && TryGetBoolProperty(serverEvent.Data, "QuietSuccess", out var quietSuccess)
            && quietSuccess)
        {
            return;
        }

        if (TryGetStringProperty(serverEvent.Data, "Message", out var message)
            && !string.IsNullOrWhiteSpace(message))
        {
            ToastService?.Show(message, fallbackType, 4000);
            return;
        }

        if (TryGetStringProperty(serverEvent.Data, "WatchSummary", out var watchSummary)
            && !string.IsNullOrWhiteSpace(watchSummary))
        {
            ToastService?.Show(watchSummary, ToastType.Warning, 4000);
        }
    }

    private static bool TryGetBoolProperty(object? data, string propertyName, out bool value)
    {
        value = false;

        if (data is not JsonElement element || element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        switch (property.ValueKind)
        {
            case JsonValueKind.True:
                value = true;
                return true;
            case JsonValueKind.False:
                value = false;
                return true;
            case JsonValueKind.String when bool.TryParse(property.GetString(), out var parsed):
                value = parsed;
                return true;
            default:
                return false;
        }
    }
}
