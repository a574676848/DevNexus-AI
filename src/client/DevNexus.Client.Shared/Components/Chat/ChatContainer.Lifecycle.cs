using DevNexus.Client.Shared.Services.Chat;
using DevNexus.Client.Shared.Models;
using DevNexus.Client.Shared.Utilities;
using DevNexus.Client.Shared.Helpers;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DevNexus.Client.Shared.Components.Chat;

/// <summary>
/// 聊天容器组件生命周期与流式健康检查。
/// </summary>
public partial class ChatContainer
{
    #region 生命周期

    protected override void OnInitialized()
    {
        _throttledRenderAction = PerformanceHelper.Throttle(
            () => _ = InvokeAsync(FlushPendingBlocksAndRenderAsync),
            TimeSpan.FromMilliseconds(RENDER_THROTTLE_MS)
        );

        _blockIndexer = new BlockIndexer();
        _lastStreamingActivityAt = DateTime.UtcNow;
        _streamingMonitorCts = new CancellationTokenSource();
        _streamingMonitorTask = MonitorStreamingHealthAsync(_streamingMonitorCts.Token);

        ChatState.OnStateChanged += HandleStateChanged;
        SessionState.OnStateChanged += HandleSessionStateChanged;
        SignalR.OnBlockReceived += HandleBlockReceived;
        SignalR.OnMessageReceived += HandleMessageReceived;
        SignalR.OnConnectionChanged += HandleConnectionChanged;
        SignalR.OnQueuedMessagesReceived += HandleQueuedMessagesReceived;
        SignalR.OnServerEvent += HandleServerEvent;

        if (!SignalR.IsConnected)
        {
            _ = SignalR.ConnectAsync();
        }
    }

    protected override Task OnInitializedAsync()
    {
        if (SessionId.HasValue)
        {
            ChatState.SetCurrentSession(SessionId.Value);
        }
        else if (ChatState.CurrentSessionId != Guid.Empty)
        {
            NavigationManager.NavigateTo($"/chat/{ChatState.CurrentSessionId}", replace: true);
        }

        return Task.CompletedTask;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        try
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("eval", "window.devnexus = window.devnexus || {}");
            await JS.InvokeVoidAsync("devnexus.registerOpenArtifactListener", _dotNetRef);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChatContainer] 注册展开事件监听器失败: {ex.Message}");
        }
    }

    protected override Task OnParametersSetAsync()
    {
        if (SessionId.HasValue && SessionId.Value != _lastLoadedSessionId)
        {
            if (ChatState.CurrentSessionId != SessionId.Value)
            {
                ChatState.SetCurrentSession(SessionId.Value);
            }

            _ = LoadSessionMessagesAsync(SessionId.Value);
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        ChatState.OnStateChanged -= HandleStateChanged;
        SessionState.OnStateChanged -= HandleSessionStateChanged;
        SignalR.OnBlockReceived -= HandleBlockReceived;
        SignalR.OnMessageReceived -= HandleMessageReceived;
        SignalR.OnConnectionChanged -= HandleConnectionChanged;
        SignalR.OnQueuedMessagesReceived -= HandleQueuedMessagesReceived;
        SignalR.OnServerEvent -= HandleServerEvent;

        try
        {
            await JS.InvokeVoidAsync("devnexus.removeOpenArtifactListener");
        }
        catch
        {
            // 忽略 JS 互操作错误
        }

        _dotNetRef?.Dispose();
        _blockIndexer?.Dispose();
        CancelConnectionRecovery();
        if (_streamingMonitorCts != null)
        {
            await _streamingMonitorCts.CancelAsync();
            _streamingMonitorCts.Dispose();
            _streamingMonitorCts = null;
        }

        if (_streamingMonitorTask != null)
        {
            try
            {
                await _streamingMonitorTask;
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
        }
    }

    /// <summary>
    /// 将缓冲区中的流式块批量应用到 UI，避免每个 block 都触发一次完整渲染。
    /// </summary>
    private Task FlushPendingBlocksAndRenderAsync()
    {
        List<DevNexus.Shared.DTOs.BlockDto>? blocksToFlush = null;

        lock (_pendingBlockSync)
        {
            if (_pendingBlocks.Count == 0)
            {
                _hasPendingBlockFlush = false;
                return Task.CompletedTask;
            }

            blocksToFlush = new List<DevNexus.Shared.DTOs.BlockDto>(_pendingBlocks);
            _pendingBlocks.Clear();
            _hasPendingBlockFlush = false;
        }

        ApplyPendingBlocks(blocksToFlush);
        StateHasChanged();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 将一批 block 合并到当前流式消息，减少频繁的小粒度 UI 变更。
    /// </summary>
    private void ApplyPendingBlocks(IReadOnlyList<DevNexus.Shared.DTOs.BlockDto> blocks)
    {
        foreach (var block in blocks)
        {
            var streamingMsg = _streamingMessage;
            if (streamingMsg?.Id != block.MessageId)
            {
                streamingMsg = _messages.FirstOrDefault(m => m.Id == block.MessageId);
            }

            if (streamingMsg == null)
            {
                streamingMsg = new ChatMessageDto
                {
                    Id = block.MessageId,
                    ChatSessionId = ChatState.CurrentSessionId,
                    SenderType = ChatConstants.RoleAssistant,
                    Content = string.Empty,
                    CreatedAt = DateTime.UtcNow,
                    Status = ChatConstants.StatusInProgress,
                    OrderedBlocks = new List<DevNexus.Shared.DTOs.BlockDto>(),
                    Artifacts = new List<ArtifactDto>()
                };
                _messages.Add(streamingMsg);
                _blockIndexer?.Clear();
            }

            _streamingMessage = streamingMsg;
            if (block.MessageId != Guid.Empty)
            {
                _currentMessageId = block.MessageId;
            }

            ApplyStreamingBlockState(block);
            _blockIndexer?.AddBlock(block);
            _lastStreamingActivityAt = DateTime.UtcNow;
            _generationTimeoutNotified = false;

            streamingMsg.Content = _blockIndexer?.GetFullContent() ?? string.Empty;
            SyncBlocks(_currentBlocks, _blockIndexer?.GetOrderedBlocks());
            SyncBlocks(streamingMsg.OrderedBlocks, _blockIndexer?.GetOrderedBlocks());
            SyncArtifacts(streamingMsg.Artifacts, _completedArtifacts);
        }
    }

    /// <summary>
    /// 周期性检查流式会话是否长时间无活动，避免后端异常后前端一直停留在旧的忙碌显示中。
    /// </summary>
    private async Task MonitorStreamingHealthAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(StreamingMonitorInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested || ChatState.CurrentSessionId == Guid.Empty)
            {
                continue;
            }

            var runState = CurrentRunPresentation.RunState;
            if (runState is ChatSessionRunState.WaitingForPendingInput or ChatSessionRunState.WaitingForApproval)
            {
                _generationTimeoutNotified = false;
                continue;
            }

            if (!CurrentRunPresentation.RunState.IsGenerationLike())
            {
                _generationTimeoutNotified = false;
                continue;
            }

            if (DateTime.UtcNow - _lastStreamingActivityAt < StreamingIdleTimeout)
            {
                continue;
            }

            await InvokeAsync(HandleStreamingTimeoutAsync);
        }
    }

    /// <summary>
    /// 流式长时间无响应时主动收口当前会话状态，避免页面持续处于假忙状态。
    /// </summary>
    private async Task HandleStreamingTimeoutAsync()
    {
        var currentSessionId = ChatState.CurrentSessionId;
        var runPresentation = currentSessionId == Guid.Empty
            ? ChatSessionRunStateDisplay.GetPresentation(ChatSessionRunState.Idle)
            : ChatState.GetSessionRunPresentation(currentSessionId);
        if (currentSessionId == Guid.Empty || !runPresentation.RunState.IsGenerationLike())
        {
            return;
        }

        var runState = runPresentation.RunState;
        if (runState is ChatSessionRunState.WaitingForPendingInput or ChatSessionRunState.WaitingForApproval)
        {
            return;
        }

        ChatState.SetSessionGeneratingOptimistic(currentSessionId, false);
        ClearBlocksWithCache();
        lock (_pendingBlockSync)
        {
            _pendingBlocks.Clear();
            _hasPendingBlockFlush = false;
        }

        if (!_generationTimeoutNotified)
        {
            _generationTimeoutNotified = true;
            await NotificationService.ShowDeduplicatedAsync(
                "生成已中断",
                "长时间未收到新内容，已自动结束当前生成。可重新发送或继续生成。",
                suppressSeconds: 10,
                dedupeKey: $"chat-generation-timeout:{currentSessionId}");
        }

        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// 取消待执行的连接恢复动作，避免短时间内重复全量恢复会话。
    /// </summary>
    private void CancelConnectionRecovery()
    {
        if (_connectionRecoveryCts == null)
        {
            return;
        }

        try
        {
            _connectionRecoveryCts.Cancel();
        }
        catch
        {
            // 忽略取消异常
        }
        finally
        {
            _connectionRecoveryCts.Dispose();
            _connectionRecoveryCts = null;
        }
    }

    #endregion
}
