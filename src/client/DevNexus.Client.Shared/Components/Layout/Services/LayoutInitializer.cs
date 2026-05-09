using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Components.Layout.Services;

/// <summary>
/// 布局初始化服务 - 负责应用启动时的初始化逻辑
/// </summary>
public class LayoutInitializer
{
    private readonly IApiService _apiService;
    private readonly IAuthService _authService;
    private readonly ISessionState _sessionState;
    private readonly IChatState _chatState;
    private readonly IUserStateService _userStateService;
    private readonly IRemoteLogService _remoteLogService;
    private System.Timers.Timer? _pingTimer;
    private const int PingIntervalMs = 30000; // 30 秒

    public LayoutInitializer(
        IApiService apiService,
        IAuthService authService,
        ISessionState sessionState,
        IChatState chatState,
        IUserStateService userStateService,
        IRemoteLogService remoteLogService)
    {
        _apiService = apiService;
        _authService = authService;
        _sessionState = sessionState;
        _chatState = chatState;
        _userStateService = userStateService;
        _remoteLogService = remoteLogService;
    }

    public event Func<int, Task>? OnLatencyChanged;

    /// <summary>
    /// 初始化应用 - 包括加载会话、刷新用户、启动 Ping
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            if (_authService.IsAuthenticated)
            {
                // 并行加载会话列表和刷新用户信息
                await Task.WhenAll(
                    LoadSessionsAsync(),
                    RefreshUserAsync()
                );
            }

            // 启动网络延迟监测
            StartPingTimer();
        }
        catch (Exception ex)
        {
            await _remoteLogService.LogErrorAsync(ex, "LayoutInitializer.InitializeAsync");
            throw;
        }
    }

    /// <summary>
    /// 刷新已认证用户的状态
    /// </summary>
    public async Task RefreshUserAsync()
    {
        try
        {
            await _userStateService.RefreshUserAsync();
        }
        catch (Exception ex)
        {
            await _remoteLogService.LogErrorAsync(ex, "LayoutInitializer.RefreshUserAsync");
            // 不阻止继续执行
        }
    }

    /// <summary>
    /// 从服务器加载会话列表
    /// </summary>
    public async Task LoadSessionsAsync()
    {
        try
        {
            var sessions = await _apiService.GetSessionsAsync();
            _sessionState.SetSessions(sessions);

            // 如果有会话但当前没有选中任何会话，进入最新的会话
            if (sessions.Any() && _chatState.CurrentSessionId == Guid.Empty)
            {
                var firstSession = sessions.OrderByDescending(s => s.UpdatedAt).First();
                _chatState.SetCurrentSession(firstSession.Id);
            }
        }
        catch (Exception ex)
        {
            await _remoteLogService.LogErrorAsync(ex, "LayoutInitializer.LoadSessionsAsync", new Dictionary<string, object?>
            {
                ["IsAuthenticated"] = _authService.IsAuthenticated,
                ["ErrorMessage"] = ex.Message
            });
        }
    }

    /// <summary>
    /// 启动定期 Ping 定时器用于测量网络延迟
    /// </summary>
    private void StartPingTimer()
    {
        _pingTimer = new System.Timers.Timer(PingIntervalMs);
        _pingTimer.Elapsed += async (s, e) => await PingServerAsync();
        _pingTimer.AutoReset = true;
        _pingTimer.Start();

        // 立即执行一次 Ping
        _ = PingServerAsync();
    }

    /// <summary>
    /// 测量网络延迟
    /// </summary>
    private async Task PingServerAsync()
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await _apiService.GetHealthAsync();
            stopwatch.Stop();

            var latency = (int)stopwatch.ElapsedMilliseconds;
            if (OnLatencyChanged != null)
                await OnLatencyChanged.Invoke(latency);
        }
        catch
        {
            // 连接失败时忽略
            if (OnLatencyChanged != null)
                await OnLatencyChanged.Invoke(0);
        }
    }

    /// <summary>
    /// 停止 Ping 定时器并释放资源
    /// </summary>
    public void Dispose()
    {
        _pingTimer?.Stop();
        _pingTimer?.Dispose();
    }

    /// <summary>
    /// 处理登出后的清理
    /// </summary>
    public async Task HandleLogoutAsync()
    {
        _sessionState.Clear();
        _chatState.ClearAll();
        _userStateService.ClearUser();
    }
}
