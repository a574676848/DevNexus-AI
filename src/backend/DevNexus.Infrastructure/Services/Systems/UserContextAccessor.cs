using DevNexus.Core.Abstractions;

namespace DevNexus.Infrastructure.Services.Systems;

/// <summary>
/// 用户上下文访问器实现
/// 使用 AsyncLocal 确保在异步调用链中正确传递用户身份
/// </summary>
public class UserContextAccessor : IUserContextAccessor
{
    private static readonly AsyncLocal<Guid?> _currentUserId = new();
    private static readonly AsyncLocal<string?> _currentSessionId = new();
    private static readonly AsyncLocal<string?> _currentConnectionId = new();

    /// <inheritdoc />
    public Guid? CurrentUserId
    {
        get => _currentUserId.Value;
        set => _currentUserId.Value = value;
    }

    /// <inheritdoc />
    public string? CurrentSessionId
    {
        get => _currentSessionId.Value;
        set => _currentSessionId.Value = value;
    }

    /// <inheritdoc />
    public string? CurrentConnectionId
    {
        get => _currentConnectionId.Value;
        set => _currentConnectionId.Value = value;
    }
}
