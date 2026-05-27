using DevNexus.Client.Shared.Helpers;
using DevNexus.Client.Shared.Models;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DevNexus.Client.Shared.Components.Chat;

/// <summary>
/// 消息列表组件 - 处理消息展示、滚动管理和流式渲染
/// </summary>
public partial class MessageList : IAsyncDisposable
{
    #region Parameters

    [Parameter] public List<ChatMessageDto> Messages { get; set; } = new();
    [Parameter] public Guid CurrentSessionId { get; set; }
    [Parameter] public List<BlockDto> CurrentBlocks { get; set; } = new();
    [Parameter] public List<ArtifactDto> CurrentArtifacts { get; set; } = new();
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public string? UserDisplayName { get; set; }
    [Parameter] public string? UserAvatarUrl { get; set; }
    [Parameter] public EventCallback<BlockDto> OnContinueGeneration { get; set; }
    [Parameter] public bool EnableVirtualization { get; set; } = true;
    [Parameter] public int VirtualizationThreshold { get; set; } = 50;
    [Parameter] public EventCallback<(ChatMessageDto Message, string NewContent)> OnEditMessage { get; set; }
    [Parameter] public EventCallback<ChatMessageDto> OnRegenerateMessage { get; set; }
    [Parameter] public IReadOnlyList<TerminalRecordState> TerminalRecords { get; set; } = Array.Empty<TerminalRecordState>();
    [Parameter] public EventCallback OnOpenTerminalDetail { get; set; }

    #endregion

    #region Injected Services

    [Inject] public IJSRuntime JS { get; set; } = default!;
    [Inject] public IChatState ChatState { get; set; } = default!;

    #endregion

    #region Private Fields

    private ElementReference _listRef;
    private int _lastMessageCount = 0;
    private bool _showScrollButton = false;
    private bool _isAtBottom = true;
    private int _unreadMessageCount = 0;
    private DotNetObjectReference<MessageList>? _dotNetRef;
    private Guid? _lastFirstMessageId = null;
    private bool _needsScrollToBottom = false;
    private Guid _lastSessionId = Guid.Empty;
    /// <summary>
    /// 用户在生成期间主动向上滚动，锁定自动滚动直到用户回到底部或主动点击回到最新
    /// </summary>
    private bool _userScrolledAway = false;
    private bool _autoFollowLatestMessage = true;
    private DateTime _autoScrollLeaseUntil = DateTime.MinValue;
    private bool ShowTerminalSummaryCard =>
        CurrentSessionId != Guid.Empty
        && ChatState.GetTerminalPresentation(CurrentSessionId) != null;
    private SessionMessagePresentationState CurrentMessagePresentation =>
        CurrentSessionId == Guid.Empty
            ? new SessionMessagePresentationState()
            : ChatState.GetSessionMessagePresentation(CurrentSessionId);
    private bool ShouldShowAiStatusIndicator =>
        ChatState != null
        && CurrentSessionId != Guid.Empty
        && (CurrentMessagePresentation.ShouldShowStatusIndicator || HasRuntimeActivity());
    private bool IsStreamingRun =>
        ChatState != null
        && CurrentSessionId != Guid.Empty
        && CurrentMessagePresentation.IsStreaming;
    private DateTime _lastAutoScrollAt = DateTime.MinValue;
    private const int InitialScrollDelayMs = 50;
    private const int AutoScrollThrottleIntervalMs = 120;
    private const int StreamingAutoScrollLeaseMs = 900;
    private const int StickyAutoScrollDurationMs = 320;
    private static readonly TimeSpan AutoScrollThrottleInterval = TimeSpan.FromMilliseconds(AutoScrollThrottleIntervalMs);
    private static readonly TimeSpan StreamingAutoScrollLease = TimeSpan.FromMilliseconds(StreamingAutoScrollLeaseMs);

    #endregion

    #region Lifecycle Methods

    protected override Task OnParametersSetAsync()
    {
        // 检测会话切换：以 SessionId 为准，避免仅靠首条消息判断
        var currentFirstMessageId = Messages?.FirstOrDefault()?.Id;

        if (_lastSessionId != CurrentSessionId)
        {
            _needsScrollToBottom = true;
            _lastSessionId = CurrentSessionId;
            _lastFirstMessageId = currentFirstMessageId;
            _lastMessageCount = 0;
            _isAtBottom = true;
            _unreadMessageCount = 0;
            _showScrollButton = false;
            _userScrolledAway = false;
            _autoFollowLatestMessage = true;
            ExtendAutoScrollLease();
            return Task.CompletedTask;
        }

        if (Messages != null && _lastFirstMessageId != currentFirstMessageId && Messages.Count > 0)
        {
            _needsScrollToBottom = true;
            _lastFirstMessageId = currentFirstMessageId;
        }

        var currentMessageCount = Messages?.Count ?? 0;
        var messageCountIncreased = currentMessageCount > _lastMessageCount;
        if (messageCountIncreased && ShouldFollowNewMessage())
        {
            _autoFollowLatestMessage = true;
            _unreadMessageCount = 0;
            _showScrollButton = false;
            _userScrolledAway = false;
            ExtendAutoScrollLease();
        }
        else if (messageCountIncreased && !_isAtBottom)
        {
            _unreadMessageCount += currentMessageCount - _lastMessageCount;
            _showScrollButton = true;
        }
        else if (currentMessageCount < _lastMessageCount)
        {
            _unreadMessageCount = 0;
        }

        return Task.CompletedTask;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // 注册滚动监听
            _dotNetRef = DotNetObjectReference.Create(this);
            try
            {
                await JS.InvokeVoidAsync("setupScrollListener", _listRef, _dotNetRef);
            }
            catch
            {
                // 忽略 JS 互操作错误
            }

            // 首次渲染时，如果已有历史消息，强制滚动到底部
            if (Messages != null && Messages.Count > 0)
            {
                _lastMessageCount = Messages.Count;
                _lastFirstMessageId = Messages.FirstOrDefault()?.Id;
                await Task.Delay(InitialScrollDelayMs);
                await ScrollToBottomAsync(force: true, sticky: true);
            }
        }
        else
        {
            // 处理会话切换时的滚动
            if (_needsScrollToBottom)
            {
                _needsScrollToBottom = false;
                _lastMessageCount = Messages?.Count ?? 0;
                await Task.Delay(InitialScrollDelayMs);
                await ScrollToBottomAsync(force: true, sticky: true);
            }
            // 非首次渲染：仅当用户仍停留在底部且未主动上滚时才跟随新内容
            else if (Messages != null && Messages.Count != _lastMessageCount)
            {
                _lastMessageCount = Messages.Count;

                if (_autoFollowLatestMessage || (_isAtBottom && !_userScrolledAway))
                {
                    ExtendAutoScrollLease();
                    await ScrollToBottomAsync(force: true, sticky: true);
                }
            }
            else if (IsStreamingRun && ShouldKeepAutoFollowing())
            {
                await ScrollToBottomIfDueAsync();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_dotNetRef != null)
        {
            try
            {
                await JS.InvokeVoidAsync("removeScrollListener", _listRef);
            }
            catch
            {
                // 忽略 JS 互操作错误
            }
            _dotNetRef.Dispose();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// JS 回调：更新滚动位置状态
    /// </summary>
    [JSInvokable]
    public void OnScrollPositionChanged(bool isAtBottom)
    {
        _isAtBottom = isAtBottom;
        if (isAtBottom)
        {
            _unreadMessageCount = 0;
            _userScrolledAway = false;
        }
        else if (IsStreamingRun && !IsWithinAutoScrollLease())
        {
            // 生成期间用户主动向上滚动，锁定自动滚动
            _userScrolledAway = true;
            _autoFollowLatestMessage = false;
        }

        var shouldShow = !isAtBottom && !_autoFollowLatestMessage && Messages.Count > 5;
        if (_showScrollButton != shouldShow)
        {
            _showScrollButton = shouldShow;
            InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>
    /// 滚动到底部
    /// </summary>
    public async Task ScrollToBottomAsync(bool force = false)
    {
        try
        {
            var didScroll = false;

            if (force)
            {
                didScroll = await JS.InvokeAsync<bool>("scrollToBottomForce", _listRef);
            }
            else
            {
                // 不再把 _isAtBottom 当 force 传给 JS，让 JS 根据实际滚动位置判断
                didScroll = await JS.InvokeAsync<bool>("scrollToBottom", _listRef, false);
            }

            if (!didScroll)
            {
                return;
            }

            _isAtBottom = true;
            _unreadMessageCount = 0;
            _showScrollButton = false;
            _userScrolledAway = false;
            _autoFollowLatestMessage = true;
            _lastAutoScrollAt = DateTime.UtcNow;
            ExtendAutoScrollLease();
        }
        catch
        {
            // 忽略 JS 互操作错误
        }
    }

    private async Task ScrollToBottomAsync(bool force, bool sticky)
    {
        try
        {
            var didScroll = sticky
                ? await JS.InvokeAsync<bool>("scrollToBottomWhileStable", _listRef, StickyAutoScrollDurationMs)
                : await JS.InvokeAsync<bool>(force ? "scrollToBottomForce" : "scrollToBottom", _listRef, force);

            if (!didScroll)
            {
                return;
            }

            _isAtBottom = true;
            _unreadMessageCount = 0;
            _showScrollButton = false;
            _userScrolledAway = false;
            _autoFollowLatestMessage = true;
            _lastAutoScrollAt = DateTime.UtcNow;
            ExtendAutoScrollLease();
        }
        catch
        {
            // 忽略 JS 互操作错误
        }
    }

    /// <summary>
    /// 仅在节流窗口到期时执行自动滚动，避免生成期间高频滚动拖慢页面。
    /// </summary>
    private async Task ScrollToBottomIfDueAsync()
    {
        var now = DateTime.UtcNow;
        if (now - _lastAutoScrollAt < AutoScrollThrottleInterval)
        {
            return;
        }

        await ScrollToBottomAsync();
    }

    private bool ShouldKeepAutoFollowing()
    {
        return !_userScrolledAway && (_isAtBottom || _autoFollowLatestMessage || IsWithinAutoScrollLease());
    }

    private bool ShouldFollowNewMessage()
    {
        var lastMessage = Messages?.LastOrDefault();
        return _isAtBottom
            || _autoFollowLatestMessage
            || ChatConstants.IsUserSender(lastMessage?.SenderType);
    }

    private void ExtendAutoScrollLease()
    {
        _autoScrollLeaseUntil = DateTime.UtcNow.Add(StreamingAutoScrollLease);
    }

    private bool IsWithinAutoScrollLease()
    {
        return DateTime.UtcNow <= _autoScrollLeaseUntil;
    }

    private bool HasRuntimeActivity()
    {
        var toolActivity = ChatState.GetToolActivityPresentation(CurrentSessionId);
        if (toolActivity?.IsActive == true)
        {
            return true;
        }

        var terminal = ChatState.GetTerminalPresentation(CurrentSessionId);
        return terminal != null && (terminal.IsActive || terminal.WaitingForInput);
    }

    /// <summary>
    /// 打开 Swarm 监控面板
    /// </summary>
    public void OpenSwarmSidekick()
    {
        if (CurrentSessionId != Guid.Empty)
        {
            ChatState?.OpenSwarmSidekick(CurrentSessionId);
        }
    }

    /// <summary>
    /// 处理截断续写请求
    /// </summary>
    public async Task HandleContinueGeneration(BlockDto truncatedBlock)
    {
        if (OnContinueGeneration.HasDelegate)
        {
            await OnContinueGeneration.InvokeAsync(truncatedBlock);
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// 处理滚动事件（Blazor 端备用，主要靠 JS）
    /// </summary>
    private void HandleScroll()
    {
        // JS 会通过 OnScrollPositionChanged 通知滚动状态
    }

    /// <summary>
    /// 判断是否是最后一条 Assistant 消息
    /// </summary>
    private bool IsLastAssistantMessage(ChatMessageDto message)
    {
        if (!ChatConstants.IsAssistantSender(message.SenderType)) return false;

        var lastAssistant = Messages.LastOrDefault(m => ChatConstants.IsAssistantSender(m.SenderType));
        return lastAssistant?.Id == message.Id;
    }

    private bool HasRenderableCurrentBlocks()
    {
        return CurrentBlocks != null && CurrentBlocks.Any(IsRenderableStreamingBlock);
    }

    private static bool IsRenderableStreamingBlock(BlockDto block)
    {
        return block.BlockType is BlockType.TextDelta
            or BlockType.Thinking
            or BlockType.Chart
            or BlockType.InteractiveCard
            or BlockType.Terminal
            or BlockType.ArtifactStart
            or BlockType.ArtifactDelta
            or BlockType.ArtifactEnd
            or BlockType.Truncated
            or BlockType.Warning
            or BlockType.Reference;
    }

    private string GetScrollButtonText()
    {
        if (_unreadMessageCount > 0)
        {
            return $"有 {_unreadMessageCount} 条新消息";
        }

        return "回到最新";
    }

    #endregion
}
