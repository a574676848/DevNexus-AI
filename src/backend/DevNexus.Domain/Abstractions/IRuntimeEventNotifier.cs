using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 统一运行时事件通知服务。
/// </summary>
public interface IRuntimeEventNotifier
{
    /// <summary>
    /// 推送结构化运行时事件。
    /// </summary>
    Task NotifyAsync(
        Guid userId,
        Guid sessionId,
        ServerEventType eventType,
        object? data = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 推送已构造的服务器事件。
    /// </summary>
    Task NotifyAsync(
        Guid userId,
        ServerEvent serverEvent,
        CancellationToken cancellationToken = default);
}
