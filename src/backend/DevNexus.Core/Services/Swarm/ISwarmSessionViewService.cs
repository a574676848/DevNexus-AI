using DevNexus.Shared.DTOs.Swarm;

namespace DevNexus.Core.Services.Swarm;

/// <summary>
/// Swarm 会话视图服务接口。
/// </summary>
public interface ISwarmSessionViewService
{
    /// <summary>
    /// 获取当前会话的工作包快照。
    /// </summary>
    Task<IReadOnlyList<ContextWorkPackageDto>> GetContextPackagesAsync(string sessionId, CancellationToken cancellationToken = default);
}
