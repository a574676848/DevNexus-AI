using Microsoft.AspNetCore.Components;
using DevNexus.Client.Shared.Models;

namespace DevNexus.Client.Shared.Components.Layout.Services;

/// <summary>
/// 布局事件处理服务 - 负责协调各种事件处理逻辑
/// </summary>
public class LayoutEventHandler
{
    private readonly IAuthService _authService;
    private readonly ISignalRService _signalR;
    private readonly LayoutInitializer _layoutInitializer;
    private readonly SessionManager _sessionManager;
    private readonly SidekickManager _sidekickManager;
    private readonly LayoutStateManager _layoutState;
    private readonly NavigationManager _navigationManager;
    private readonly IChatState _chatState;
    private readonly ISessionState _sessionState;
    private readonly IRemoteLogService _remoteLogService;

    public event Func<Task>? OnStateChanged;

    public LayoutEventHandler(
        IAuthService authService,
        ISignalRService signalR,
        LayoutInitializer layoutInitializer,
        SessionManager sessionManager,
        SidekickManager sidekickManager,
        LayoutStateManager layoutState,
        NavigationManager navigationManager,
        IChatState chatState,
        ISessionState sessionState,
        IRemoteLogService remoteLogService)
    {
        _authService = authService;
        _signalR = signalR;
        _layoutInitializer = layoutInitializer;
        _sessionManager = sessionManager;
        _sidekickManager = sidekickManager;
        _layoutState = layoutState;
        _navigationManager = navigationManager;
        _chatState = chatState;
        _sessionState = sessionState;
        _remoteLogService = remoteLogService;
    }

    /// <summary>
    /// 注册所有事件处理器
    /// </summary>
    public void RegisterEventHandlers()
    {
        _layoutInitializer.OnLatencyChanged += HandleLatencyChanged;
        _sessionManager.OnSessionsChanged += HandleSessionsChanged;
        _sidekickManager.OnStateChanged += HandleSidekickStateChanged;
        _layoutState.OnStateChanged += HandleLayoutStateChanged;
    }

    /// <summary>
    /// 注销所有事件处理器
    /// </summary>
    public void UnregisterEventHandlers()
    {
        _layoutInitializer.OnLatencyChanged -= HandleLatencyChanged;
        _sessionManager.OnSessionsChanged -= HandleSessionsChanged;
        _sidekickManager.OnStateChanged -= HandleSidekickStateChanged;
        _layoutState.OnStateChanged -= HandleLayoutStateChanged;
    }

    /// <summary>
    /// 处理认证状态变化
    /// </summary>
    public async Task HandleAuthStateChangedAsync(bool isAuthenticated)
    {
        if (isAuthenticated)
        {
            // 登录成功：重新连接 SignalR（使用新 Token）
            try
            {
                await _signalR.DisconnectAsync();
                await _signalR.ConnectAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SignalR 重连失败: {ex.Message}");
            }

            await _layoutInitializer.LoadSessionsAsync();
            await _layoutInitializer.RefreshUserAsync();
        }
        else
        {
            // 登出：彻底清理所有用户相关状态
            try
            {
                await _signalR.DisconnectAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SignalR 断开失败: {ex.Message}");
            }

            await _layoutInitializer.HandleLogoutAsync();
            _navigationManager.NavigateTo("/login", forceLoad: true);
        }

        await TriggerStateChangeAsync();
    }

    /// <summary>
    /// 处理路由变化
    /// </summary>
    public async Task HandleLocationChangedAsync(string uri)
    {
        var uriObj = new Uri(uri);
        var path = uriObj.AbsolutePath.ToLower();
        _layoutState.UpdateSidebarVisibility(uri);

        // 离开聊天页面时自动关闭分屏
        _sidekickManager.TryCloseIfNotOnChatPage(path);

        await _sidekickManager.SyncStateToJS();
        await TriggerStateChangeAsync();
    }

    /// <summary>
    /// 处理窗口最大化状态变化
    /// </summary>
    public void HandleMaximizedChanged(bool isMaximized)
    {
        _layoutState.IsMaximized = isMaximized;
    }

    /// <summary>
    /// 处理聊天状态变化
    /// </summary>
    public async Task HandleChatStateChangedAsync()
    {
        await _sidekickManager.SyncStateToJS();
        await TriggerStateChangeAsync();
    }

    /// <summary>
    /// 处理会话状态变化
    /// </summary>
    public void HandleSessionStateChanged()
    {
        HandleSessionsChanged();
    }

    /// <summary>
    /// 处理 ErrorBoundary 错误恢复
    /// </summary>
    public async Task HandleErrorRecoveryAsync(Exception exception)
    {
        try
        {
            await _remoteLogService.LogErrorAsync(
                exception,
                "ErrorBoundary",
                new Dictionary<string, object?>
                {
                    ["Action"] = "Caught",
                    ["Message"] = exception.Message,
                    ["StackTrace"] = exception.StackTrace,
                    ["InnerException"] = exception.InnerException?.Message
                });
        }
        catch
        {
            System.Diagnostics.Debug.WriteLine($"[ErrorBoundary] 捕获到异常: {exception.Message}");
        }

        _layoutState.ResetErrorBoundary();
        await TriggerStateChangeAsync();
    }

    private async Task HandleLatencyChanged(int latency)
    {
        _layoutState.Latency = latency;
    }

    private void HandleSessionsChanged()
    {
        _ = TriggerStateChangeAsync();
    }

    private void HandleSidekickStateChanged()
    {
        _ = TriggerStateChangeAsync();
    }

    private void HandleLayoutStateChanged()
    {
        _ = TriggerStateChangeAsync();
    }

    private async Task TriggerStateChangeAsync()
    {
        if (OnStateChanged != null)
        {
            await OnStateChanged.Invoke();
        }
    }

    /// <summary>
    /// 获取会话列表项
    /// </summary>
    public List<(Guid Id, string Title, DateTime UpdatedAt, string? LastMessage, int MessageCount, bool IsPinned, SessionRunPresentationState RunPresentation)> GetSessionItems()
    {
        return _sessionManager.GetSessionItems();
    }
}
