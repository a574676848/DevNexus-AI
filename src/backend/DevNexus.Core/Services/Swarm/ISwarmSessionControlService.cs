using DevNexus.Shared.Enums;
using DevNexus.Shared.DTOs.Swarm;

namespace DevNexus.Core.Services.Swarm;

/// <summary>
/// Swarm 会话控制服务接口。
/// </summary>
public interface ISwarmSessionControlService
{
    /// <summary>
    /// 暂停指定会话。
    /// </summary>
    Task PauseAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 恢复指定会话。
    /// </summary>
    Task ResumeAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 中止指定会话。
    /// </summary>
    Task AbortAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 重试指定会话中的失败工作包。
    /// </summary>
    Task<SwarmControlCommandDto> RetryPackageAsync(string sessionId, string packageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前会话控制状态。
    /// </summary>
    SwarmControlStatus? GetStatus(string sessionId);

    /// <summary>
    /// 获取当前会话控制状态快照。
    /// </summary>
    SwarmSessionControlSnapshot GetSnapshot(string sessionId);
}
