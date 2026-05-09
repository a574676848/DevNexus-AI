namespace DevNexus.Core.Abstractions;

/// <summary>
/// 用户上下文访问器接口
/// 基于 AsyncLocal 在异步调用链中传递当前用户ID
/// </summary>
public interface IUserContextAccessor
{
    /// <summary>
    /// 获取或设置当前异步上下文中的用户ID
    /// </summary>
    Guid? CurrentUserId { get; set; }

    /// <summary>
    /// 获取或设置当前异步上下文中的会话ID (Phase 2+3 用于 Shell 持久化)
    /// </summary>
    string? CurrentSessionId { get; set; }

    /// <summary>
    /// 获取或设置当前异步上下文中的连接ID
    /// </summary>
    string? CurrentConnectionId { get; set; }
}
