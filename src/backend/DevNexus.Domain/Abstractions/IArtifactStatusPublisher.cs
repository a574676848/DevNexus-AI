using DevNexus.Shared.DTOs;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// Artifact 状态发布服务接口，用于解耦后台任务与 SignalR Hub
/// </summary>
public interface IArtifactStatusPublisher
{
    /// <summary>
    /// 发布状态更新
    /// </summary>
    Task PublishStatusAsync(string userId, string traceId, string status, SmartDocument? doc);
}
