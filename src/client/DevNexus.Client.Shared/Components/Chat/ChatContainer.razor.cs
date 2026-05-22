using DevNexus.Client.Shared.Services.Chat;
using DevNexus.Client.Shared.Abstractions;
using DevNexus.Client.Shared.Helpers;
using DevNexus.Client.Shared.Models;
using DevNexus.Client.Shared.Utilities;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using DevNexus.Shared.Constants;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DevNexus.Client.Shared.Components.Chat;

/// <summary>
/// 聊天容器组件 - 仅负责 UI 状态管理和事件分发，业务逻辑委托给各服务
/// </summary>
public partial class ChatContainer : IAsyncDisposable
{
    [Inject] private IApiService ApiService { get; set; } = default!;

    [Parameter] public Guid? SessionId { get; set; }

    // UI 本地状态
    private List<ChatMessageDto> _messages = new();
    private List<BlockDto> _currentBlocks = new();
    private bool _isArtifactOpen = false;
    private ArtifactDto? _currentArtifact;
    private List<ArtifactDto> _completedArtifacts = new();
    private Guid _currentMessageId;
    private string _sessionTitle = "新会话";
    private bool _isFirstMessage = true;
    private Guid _lastLoadedSessionId = Guid.Empty;
    private DotNetObjectReference<ChatContainer>? _dotNetRef;
    private bool _isLoadingMessages = false;
    private Guid? _currentSessionProviderId = null;
    // 消息列表组件引用（用于主动滚动）
    private MessageList? _messageList;
    private ChatMessageDto? _streamingMessage;

    // 流式渲染节流控制
    private Action? _throttledRenderAction;
    private const int RENDER_THROTTLE_MS = 80;
    private static readonly TimeSpan SessionLoadTimeout = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan StreamingIdleTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan StreamingMonitorInterval = TimeSpan.FromSeconds(5);
    private readonly object _pendingBlockSync = new();
    private readonly List<BlockDto> _pendingBlocks = new();
    private bool _hasPendingBlockFlush;
    private CancellationTokenSource? _streamingMonitorCts;
    private Task? _streamingMonitorTask;
    private CancellationTokenSource? _connectionRecoveryCts;
    private DateTime _lastStreamingActivityAt = DateTime.UtcNow;
    private bool _generationTimeoutNotified;

    // 🔥 P1-5 性能优化：差量块索引器 - O(1) 增量处理
    private BlockIndexer? _blockIndexer;

    private ChatSessionDto? CurrentSession =>
        ChatState.CurrentSessionId == Guid.Empty
            ? null
            : SessionState.Sessions.FirstOrDefault(session => session.Id == ChatState.CurrentSessionId);

    private TerminalPresentationState? CurrentTerminalPresentation =>
        ChatState.CurrentSessionId == Guid.Empty
            ? null
            : ChatState.GetTerminalPresentation(ChatState.CurrentSessionId);

    private SessionRunPresentationState CurrentRunPresentation =>
        ChatState.CurrentSessionId == Guid.Empty
            ? ChatSessionRunStateDisplay.GetPresentation(ChatSessionRunState.Idle)
            : ChatState.GetSessionRunPresentation(ChatState.CurrentSessionId);

    private bool HasActiveCliWorkbenchHandoff => CurrentTerminalPresentation?.IsActive == true;

    private string WorkbenchTerminalEyebrow => "终端概览";

    private string WorkbenchTerminalHeadline => "聊天是唯一执行入口";

    private string WorkbenchTerminalStatus =>
        CurrentTerminalPresentation?.StatusLabel ?? "运行中";

    private string WorkbenchTerminalDescription =>
        CurrentTerminalPresentation?.Description ?? "终端仍在运行，可随时查看。";

    private string ActiveWorkbenchTerminalMode => CurrentTerminalPresentation?.ModeLabel ?? "聊天执行";

    private IReadOnlyList<TerminalRecordState> CurrentTerminalRecords => ChatState.CurrentTerminalRecords;

    private bool IsTerminalPanelVisible =>
        ChatState.IsSidekickVisible && ChatState.CurrentSidekickPane == SidekickPaneKind.ChatTerminal;

    private TerminalRecordState? HeaderTerminalRecord =>
        CurrentTerminalRecords.FirstOrDefault(record => record.IsActive)
        ?? ChatState.CurrentFocusedTerminalRecord
        ?? CurrentTerminalRecords.FirstOrDefault();

    private string ActiveWorkbenchTerminalScope =>
        CurrentTerminalPresentation?.ScopeLabel ?? $"会话 {FormatCompactId(ChatState.CurrentSessionId)}";

    private string? ActiveWorkbenchTerminalSessionState =>
        CurrentTerminalPresentation?.RunStateLabel;

    private string? ActiveWorkbenchTerminalWorkingDirectory =>
        CurrentTerminalPresentation?.WorkingDirectory;

    private string? ActiveWorkbenchTerminalCommand =>
        CurrentTerminalPresentation?.Command;

    private bool ActiveWorkbenchTerminalWaitingForInput => CurrentTerminalPresentation?.WaitingForInput == true;

    private bool _isQueueOperationInFlight;
    private bool _isQueueExpanded;

    private void HandleSessionStateChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    #region 会话加载与状态恢复

    /// <summary>
    /// 加载会话历史消息（委托 MessageHandlingService 处理 API 调用和 Swarm 检测）
    /// </summary>
    private async Task LoadSessionMessagesAsync(Guid sessionId)
    {
        if (_isLoadingMessages && _lastLoadedSessionId == sessionId) return;

        var shouldRefreshAfterLoading = false;

        try
        {
            _isLoadingMessages = true;
            shouldRefreshAfterLoading = true;
            _lastLoadedSessionId = sessionId;
            _messages.Clear();
            ClearBlocksWithCache();
            ChatState.ClearSessionRuntime(sessionId);
            ChatState.ClearAgentTurnEvents(sessionId);
            _streamingMessage = null;
            _isFirstMessage = true;
            ChatState.ClearCliExecSession(sessionId);
            ChatState.ClearTerminalRecords(sessionId);
            ChatState.CloseTerminalModal();

            // 从 SessionState 获取会话标题（立即显示）
            var session = SessionState.Sessions.FirstOrDefault(s => s.Id == sessionId);
            _sessionTitle = session?.Title ?? "新会话";
            StateHasChanged();

            var loadTask = LoadSessionMessagesCoreAsync(sessionId, session);
            var completedTask = await Task.WhenAny(loadTask, Task.Delay(SessionLoadTimeout));
            if (completedTask != loadTask)
            {
                await RemoteLogService.LogErrorAsync(
                    new TimeoutException("会话消息加载超时"),
                    "ChatContainer.LoadSessionMessagesAsync.Timeout",
                    new Dictionary<string, object?>
                    {
                        ["SessionId"] = sessionId,
                        ["TimeoutSeconds"] = SessionLoadTimeout.TotalSeconds
                    });

                _isLoadingMessages = false;
                shouldRefreshAfterLoading = false;
                await NotificationService.ShowDeduplicatedAsync(
                    "会话恢复超时",
                    "历史消息加载时间过长，已停止等待。你可以刷新会话或重新进入。",
                    suppressSeconds: 12,
                    dedupeKey: $"session-load-timeout:{sessionId}");

                StateHasChanged();
                return;
            }

            var messages = await loadTask;

            // 加载完成后检查是否仍是当前会话（防止竞态条件）
            if (_lastLoadedSessionId != sessionId)
            {
                Console.WriteLine($"[ChatContainer] 会话已切换，丢弃旧数据: {sessionId} -> {_lastLoadedSessionId}");
                return;
            }

            _messages = messages;
            _isFirstMessage = _messages.Count == 0;
            ChatState.MergeTerminalHistory(sessionId, _messages);

            await RestoreQueuedMessagesAsync(sessionId);
            await RestoreActiveTerminalRecordsAsync(sessionId);
            await RefreshRuntimeSnapshotAsync(
                sessionId,
                refreshCliExecSession: true,
                refreshPendingInteractions: true);

            // 如果该会话正在生成中，恢复流式状态
            if (ChatState.GetSessionRunPresentation(sessionId).RunState.IsGenerationLike())
            {
                var idsToRemove = MessageHandlingService.RestoreGeneratingState(sessionId, _currentBlocks);
                foreach (var id in idsToRemove)
                {
                    _messages.RemoveAll(m => m.Id == id);
                }
            }

            _isLoadingMessages = false;
            shouldRefreshAfterLoading = false;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载消息失败: {ex.Message}");
        }
        finally
        {
            if (shouldRefreshAfterLoading)
            {
                _isLoadingMessages = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    /// <summary>
    /// 处理发送消息（带 Provider 选择和 Artifact ID）
    /// </summary>
    private async Task HandleSendWithProviderAsync(
        ChatComposerSubmission args)
    {
        var content = args.Content;
        var providerId = args.ProviderId;
        var artifactIds = args.ArtifactIds;
        var artifacts = args.Artifacts;
        var enableRag = args.EnableRag;

        if (providerId.HasValue)
        {
            _currentSessionProviderId = providerId;
        }

        // 委托服务构建用户消息并发送
        var userMessage = await GenerationControlService.HandleSendWithProviderAsync(
            content,
            ChatState.CurrentSessionId,
            providerId,
            artifactIds,
            artifacts,
            enableRag,
            args.SelectedSkillName,
            args.Metadata);

        if (userMessage == null) return;

        // 本地 UI 状态更新
        _messages.Add(userMessage);
        ClearBlocksWithCache();
        _completedArtifacts.Clear();
        _currentArtifact = null;
        _currentMessageId = Guid.NewGuid();
        StateHasChanged();

        _ = PromoteOptimisticUserMessageAsync(userMessage.Id);

        // 发送消息后强制滚动到最新
        if (_messageList != null)
            await _messageList.ScrollToBottomAsync(true);
    }

    /// <summary>
    /// 处理取消生成
    /// </summary>
    private async Task HandleCancelAsync()
    {
        var currentSessionId = ChatState.CurrentSessionId;

        var currentCliSession = ChatState.GetCliExecSession(currentSessionId);
        if (currentCliSession?.IsActive == true)
        {
            // 终端会话停止时也立即回落会话运行态，避免按钮状态滞后等待后端回包。
            ChatState.SetSessionGeneratingOptimistic(currentSessionId, false);
            StateHasChanged();
            var terminateResult = await SignalR.TerminateCliSessionAsync(currentSessionId);
            if (terminateResult?.State != null)
            {
                ChatState.UpdateCliExecSession(terminateResult.State);
            }
            await SignalR.CancelGenerationAsync(currentSessionId);
            return;
        }

        // 立即停止当前会话运行态，确保按钮即时响应
        ChatState.SetSessionGeneratingOptimistic(currentSessionId, false);
        StateHasChanged();

        // 委托服务处理 Swarm 中止、内容固化、SignalR 取消
        var solidifiedMessage = await GenerationControlService.HandleCancelAsync(
            currentSessionId, _currentBlocks, _currentMessageId);

        if (solidifiedMessage != null)
        {
            var existingIndex = _messages.FindIndex(m => m.Id == solidifiedMessage.Id);
            if (existingIndex >= 0)
            {
                _messages[existingIndex] = solidifiedMessage;
            }
            else
            {
                _messages.Add(solidifiedMessage);
            }
        }

        ClearBlocksWithCache();
        StateHasChanged();
    }

    #endregion

    #region 消息编辑与重生成

    /// <summary>
    /// 处理消息编辑并重新发送
    /// </summary>
    private async Task HandleEditMessageAsync((ChatMessageDto message, string newContent) args)
    {
        var (message, newContent) = args;

        if (ChatState.CurrentSessionId == Guid.Empty) return;

        var index = _messages.FindIndex(m => m.Id == message.Id);
        if (index == -1) return;

        // ★ 必须先调服务，此时 _messages 完整，服务内部 FindIndex 才能找到原始消息
        // 若先截断 _messages 再传入，服务找不到消息会提前 return，导致消息清空且不发送
        var deletedIds = await MessageEditingService.HandleEditMessageAsync(
            message, newContent ?? string.Empty,
            ChatState.CurrentSessionId, _currentSessionProviderId, _messages);

        // 服务调用完成（后端删除 + SignalR fire-and-forget 已触发）后再做本地 UI 截断
        _messages.RemoveRange(index, _messages.Count - index);
        ClearBlocksWithCache();

        // 乐观显示编辑后的用户消息（与 HandleSendWithProviderAsync 保持一致）
        _messages.Add(new ChatMessageDto
        {
            Id = Guid.NewGuid(),
            ChatSessionId = ChatState.CurrentSessionId,
            SenderType = ChatConstants.RoleUser,
            Content = newContent ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            Artifacts = CloneArtifacts(message.Artifacts),
            Metadata = BuildReplayMetadata(message.Metadata, "已重发", "replay")
        });

        _completedArtifacts.Clear();
        _currentArtifact = null;
        _currentMessageId = Guid.NewGuid();
        StateHasChanged();

        // 编辑消息后强制滚动到最新
        if (_messageList != null)
            await _messageList.ScrollToBottomAsync(true);
    }

    /// <summary>
    /// 处理重新生成
    /// </summary>
    private async Task HandleRegenerateAsync(ChatMessageDto message)
    {
        if (!ChatConstants.IsAssistantSender(message.SenderType)) return;

        var index = _messages.FindIndex(m => m.Id == message.Id);
        if (index <= 0) return;

        var previousUserMsg = _messages[index - 1];
        if (!ChatConstants.IsUserSender(previousUserMsg.SenderType) || string.IsNullOrEmpty(previousUserMsg.Content)) return;

        var contentToResend = previousUserMsg.Content;

        // ★ 必须先调服务，此时 _messages 完整，服务内部 FindIndex 才能找到 assistant 消息
        // 若先删除再传入，服务找不到消息会提前 return，导致消息清空且不重新生成
        await MessageEditingService.HandleRegenerateAsync(
            message, ChatState.CurrentSessionId, _currentSessionProviderId, _messages);

        // 服务调用完成后再做本地 UI 截断（删掉 assistant 消息和前一条 user 消息）
        _messages.RemoveAt(index);
        _messages.RemoveAt(index - 1);

        // 乐观显示重新发送的用户消息
        _messages.Add(new ChatMessageDto
        {
            Id = Guid.NewGuid(),
            ChatSessionId = ChatState.CurrentSessionId,
            SenderType = ChatConstants.RoleUser,
            Content = contentToResend,
            CreatedAt = DateTime.UtcNow,
            Artifacts = CloneArtifacts(previousUserMsg.Artifacts),
            Metadata = BuildReplayMetadata(previousUserMsg.Metadata, "已重发", "replay")
        });

        ClearBlocksWithCache();
        _completedArtifacts.Clear();
        _currentArtifact = null;
        _currentMessageId = Guid.NewGuid();
        StateHasChanged();

        // 重新生成后强制滚动到最新
        if (_messageList != null)
            await _messageList.ScrollToBottomAsync(true);
    }

    /// <summary>
    /// 清空块及其缓存 - P1-5 性能优化辅助方法
    /// </summary>
    private void ClearBlocksWithCache()
    {
        _currentBlocks.Clear();
        _blockIndexer?.Clear();
        _streamingMessage = null;
    }

    private async Task RestoreQueuedMessagesAsync(Guid sessionId)
    {
        if (!SignalR.IsChatConnected)
        {
            return;
        }

        try
        {
            await SignalR.GetQueuedMessagesAsync(sessionId);
        }
        catch (Exception ex)
        {
            await RemoteLogService.LogErrorAsync(ex, "ChatContainer.RestoreQueuedMessagesAsync", new Dictionary<string, object?>
            {
                ["SessionId"] = sessionId
            });
        }
    }

    /// <summary>
    /// 单独恢复会话当前仍活跃的终端记录，避免依赖历史消息接口重建活跃 CLI 详情。
    /// </summary>
    private async Task RestoreActiveTerminalRecordsAsync(Guid sessionId)
    {
        try
        {
            var records = await ApiService.GetActiveTerminalRecordsAsync(sessionId);
            ChatState.SyncActiveTerminalRecords(sessionId, records);
        }
        catch (Exception ex)
        {
            await RemoteLogService.LogErrorAsync(ex, "ChatContainer.RestoreActiveTerminalRecordsAsync", new Dictionary<string, object?>
            {
                ["SessionId"] = sessionId
            });
        }
    }

    /// <summary>
    /// 执行实际的会话消息加载与恢复流程，供超时控制包装调用。
    /// </summary>
    private async Task<List<ChatMessageDto>> LoadSessionMessagesCoreAsync(Guid sessionId, ChatSessionDto? session)
    {
        // 委托服务加载消息（含 Swarm 检测和状态恢复）
        var messages = await MessageHandlingService.LoadSessionMessagesAsync(sessionId);

        _messages = messages;
        _isFirstMessage = _messages.Count == 0;
        ChatState.MergeTerminalHistory(sessionId, _messages);

        await RestoreQueuedMessagesAsync(sessionId);
        await RefreshRuntimeSnapshotAsync(
            sessionId,
            refreshCliExecSession: true,
            refreshPendingInteractions: true);

        // 如果该会话正在生成中，恢复流式状态
        if (ChatState.GetSessionRunPresentation(sessionId).RunState.IsGenerationLike())
        {
            var idsToRemove = MessageHandlingService.RestoreGeneratingState(sessionId, _currentBlocks);
            foreach (var id in idsToRemove)
            {
                _messages.RemoveAll(m => m.Id == id);
            }
        }

        return messages;
    }

    private async Task CancelQueuedMessageAsync(Guid queuedMessageId)
    {
        if (_isQueueOperationInFlight || ChatState.CurrentSessionId == Guid.Empty || queuedMessageId == Guid.Empty)
        {
            return;
        }

        _isQueueOperationInFlight = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            await SignalR.CancelQueuedMessageAsync(ChatState.CurrentSessionId, queuedMessageId);
        }
        catch (Exception ex)
        {
            await RemoteLogService.LogErrorAsync(ex, "ChatContainer.CancelQueuedMessageAsync", new Dictionary<string, object?>
            {
                ["SessionId"] = ChatState.CurrentSessionId,
                ["QueuedMessageId"] = queuedMessageId
            });
            await NotificationService.ShowDeduplicatedAsync(
                "取消排队失败",
                "当前排队消息取消失败，请稍后重试。",
                suppressSeconds: 8,
                dedupeKey: $"queue-cancel-failed:{ChatState.CurrentSessionId}");
        }
        finally
        {
            _isQueueOperationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ClearQueuedMessagesAsync()
    {
        if (_isQueueOperationInFlight || ChatState.CurrentSessionId == Guid.Empty || !ChatState.CurrentQueuedMessages.Any())
        {
            return;
        }

        _isQueueOperationInFlight = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            await SignalR.ClearQueuedMessagesAsync(ChatState.CurrentSessionId);
        }
        catch (Exception ex)
        {
            await RemoteLogService.LogErrorAsync(ex, "ChatContainer.ClearQueuedMessagesAsync", new Dictionary<string, object?>
            {
                ["SessionId"] = ChatState.CurrentSessionId
            });
            await NotificationService.ShowDeduplicatedAsync(
                "清空队列失败",
                "当前会话的等待队列清空失败，请稍后重试。",
                suppressSeconds: 8,
                dedupeKey: $"queue-clear-failed:{ChatState.CurrentSessionId}");
        }
        finally
        {
            _isQueueOperationInFlight = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void ToggleQueueExpanded()
    {
        _isQueueExpanded = !_isQueueExpanded;
    }

    /// <summary>
    /// 构建重发类消息的元数据，便于界面展示回放确认动画与状态标签。
    /// </summary>
    private static Dictionary<string, object>? BuildReplayMetadata(
        Dictionary<string, object>? sourceMetadata,
        string deliveryState,
        string entryAnimation)
    {
        var metadata = sourceMetadata != null
            ? new Dictionary<string, object>(sourceMetadata, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        metadata[ChatMessageMetadataKeys.ClientDeliveryState] = deliveryState;
        metadata[ChatMessageMetadataKeys.ClientDeliveryTone] = ChatMessageMetadataKeys.DeliveryToneSuccess;
        metadata[ChatMessageMetadataKeys.ClientEntryAnimation] = entryAnimation;

        return metadata;
    }

    private static List<ArtifactDto>? CloneArtifacts(List<ArtifactDto>? artifacts)
    {
        if (artifacts == null || artifacts.Count == 0)
        {
            return null;
        }

        return artifacts.Select(artifact => new ArtifactDto
        {
            ArtifactId = artifact.ArtifactId,
            SemanticId = artifact.SemanticId,
            Version = artifact.Version,
            BaseVersion = artifact.BaseVersion,
            Type = artifact.Type,
            Name = artifact.Name,
            Content = artifact.Content,
            FileAssetId = artifact.FileAssetId,
            FileVersionId = artifact.FileVersionId,
            ParentArtifactId = artifact.ParentArtifactId,
            MessageId = artifact.MessageId,
            SessionId = artifact.SessionId,
            CreatedAt = artifact.CreatedAt,
            UpdatedAt = artifact.UpdatedAt,
            Metadata = artifact.Metadata != null
                ? new Dictionary<string, object>(artifact.Metadata, StringComparer.OrdinalIgnoreCase)
                : null
        }).ToList();
    }

    /// <summary>
    /// 处理截断续写
    /// </summary>
    private async Task HandleContinueGenerationAsync(BlockDto truncatedBlock)
    {
        if (CurrentRunPresentation.RunState.IsGenerationLike()) return;

        _currentBlocks.Clear();
        _completedArtifacts.Clear();
        _currentArtifact = null;
        _currentMessageId = Guid.NewGuid();
        StateHasChanged();

        await MessageEditingService.HandleContinueGenerationAsync(
            truncatedBlock, ChatState.CurrentSessionId, _currentSessionProviderId);
    }

    #endregion
}
