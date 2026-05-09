using DevNexus.Shared.DTOs.Auth;
using DevNexus.Client.Shared.Abstractions;

namespace DevNexus.Client.Shared.Services.State;

/// <summary>
/// 用户状态服务实现
/// </summary>
public class UserStateService : IUserStateService
{
    private readonly IApiService _apiService;
    private readonly IUrlService _urlService;
    private readonly IRemoteLogService _remoteLog;

    private UserInfo? _currentUser;

    public UserInfo? CurrentUser => _currentUser;

    public event Func<UserInfo?, Task>? OnUserChanged;

    public UserStateService(
        IApiService apiService,
        IUrlService urlService,
        IRemoteLogService remoteLog)
    {
        _apiService = apiService;
        _urlService = urlService;
        _remoteLog = remoteLog;
    }

    /// <inheritdoc />
    public async Task UpdateUserAsync(UserInfo? user)
    {
        _currentUser = user;

        // 通知所有订阅者
        if (OnUserChanged != null)
        {
            var handlers = OnUserChanged.GetInvocationList();
            foreach (Func<UserInfo?, Task> handler in handlers)
            {
                try
                {
                    await handler(_currentUser);
                }
                catch (Exception ex)
                {
                    await _remoteLog.LogErrorAsync(ex, "UserStateService.OnUserChanged.HandlerError", new Dictionary<string, object?>
                    {
                        ["HandlerType"] = handler.Method.DeclaringType?.FullName,
                        ["HandlerMethod"] = handler.Method.Name
                    });
                }
            }
        }
    }

    /// <inheritdoc />
    public async Task RefreshUserAsync()
    {
        try
        {
            var user = await _apiService.GetCurrentUserAsync();
            await UpdateUserAsync(user);
        }
        catch (Exception ex)
        {
            await _remoteLog.LogErrorAsync(ex, "UserStateService.RefreshUserAsync");
        }
    }

    /// <inheritdoc />
    public void ClearUser()
    {
        // Fire-and-forget: keep caller API sync, but don't drop handler tasks/exceptions.
        _ = UpdateUserAsync(null);
    }

    /// <inheritdoc />
    public string? GetAvatarUrl(string? avatarUrl)
    {
        // 委托给 IUrlService 处理
        return _urlService.GetFullUrl(avatarUrl);
    }
}

