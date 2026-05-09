using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using DevNexus.Client.Shared.Abstractions;

namespace DevNexus.Client.Shared.Services.Auth;

/// <summary>
/// DevNexus 自定义认证状态提供者
/// 桥接 IAuthService 与 Blazor 授权系统
///
/// 启动行为：
/// - 首次调用时启动并等待会话恢复
/// - 配合 AuthorizeRouteView 的 Authorizing 视图显示加载状态
/// </summary>
public class DevNexusAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly IAuthService _authService;
    private static readonly TimeSpan InitializationTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// 初始化状态标志
    /// 0: 未开始, 1: 正在初始化, 2: 已完成
    /// </summary>
    private int _initializationState = 0;

    /// <summary>
    /// 初始化完成信号
    /// </summary>
    private readonly TaskCompletionSource<bool> _initializationComplete = new();

    /// <summary>
    /// 未认证状态
    /// </summary>
    private static readonly AuthenticationState AnonymousState =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public DevNexusAuthenticationStateProvider(
        IAuthService authService)
    {
        _authService = authService;

        // 订阅认证状态变更事件
        _authService.OnAuthStateChanged += OnAuthStateChanged;
    }

    /// <summary>
    /// 获取当前认证状态
    /// </summary>
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // 首次调用时启动后台会话恢复
        if (Interlocked.CompareExchange(ref _initializationState, 1, 0) == 0)
        {
            _ = InitializeSessionAsync();
        }

        // 只等待有限时间，避免会话恢复或远程刷新卡住时页面一直停留在 Authorizing
        var completed = await Task.WhenAny(_initializationComplete.Task, Task.Delay(InitializationTimeout));
        if (completed != _initializationComplete.Task)
        {
            return BuildCurrentAuthenticationState();
        }

        // 返回当前状态
        return BuildCurrentAuthenticationState();
    }

    /// <summary>
    /// 后台异步初始化会话
    /// </summary>
    private async Task InitializeSessionAsync()
    {
        try
        {
            var restored = await _authService.TryRestoreSessionAsync();

            // 标记初始化完成
            Interlocked.Exchange(ref _initializationState, 2);
            _initializationComplete.TrySetResult(restored);

            // 如果恢复成功，通知 Blazor 刷新认证状态
            if (restored)
            {
                NotifyAuthenticationStateChanged(Task.FromResult(BuildCurrentAuthenticationState()));
            }
        }
        catch
        {
            Interlocked.Exchange(ref _initializationState, 2);
            _initializationComplete.TrySetResult(false);
        }
    }

    /// <summary>
    /// 构建当前认证状态
    /// </summary>
    private AuthenticationState BuildCurrentAuthenticationState()
    {
        if (!_authService.IsAuthenticated)
        {
            return AnonymousState;
        }

        // 构建已认证的 ClaimsPrincipal
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _authService.CurrentUserId?.ToString() ?? "")
        };
        
        // 添加角色声明
        foreach (var role in _authService.CurrentUserRoles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, authenticationType: "jwt");
        var principal = new ClaimsPrincipal(identity);

        return new AuthenticationState(principal);
    }

    /// <summary>
    /// 处理认证状态变更
    /// </summary>
    private void OnAuthStateChanged(bool isAuthenticated)
    {
        Interlocked.Exchange(ref _initializationState, 2);
        _initializationComplete.TrySetResult(isAuthenticated);

        // 通知 Blazor 授权系统状态已变更
        NotifyAuthenticationStateChanged(Task.FromResult(BuildCurrentAuthenticationState()));
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _authService.OnAuthStateChanged -= OnAuthStateChanged;
        GC.SuppressFinalize(this);
    }
}
