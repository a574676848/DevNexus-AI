namespace DevNexus.Core.Services.AuthUseCases;

/// <summary>
/// 全设备登出命令处理器接口。
/// </summary>
internal interface ILogoutAllDevicesCommandHandler
{
    Task HandleAsync(Guid currentUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 处理全设备登出命令。
/// </summary>
internal sealed class LogoutAllDevicesCommandHandler : ILogoutAllDevicesCommandHandler
{
    private readonly IAuthService _authService;

    public LogoutAllDevicesCommandHandler(IAuthService authService)
    {
        _authService = authService;
    }

    public Task HandleAsync(Guid currentUserId, CancellationToken cancellationToken = default)
        => _authService.LogoutAllDevicesAsync(currentUserId, cancellationToken);
}
