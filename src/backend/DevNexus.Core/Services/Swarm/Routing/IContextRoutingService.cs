using DevNexus.Domain.Models.Swarm;

namespace DevNexus.Core.Services.Swarm.Routing;

/// <summary>
/// 上下文路由服务接口。
/// </summary>
public interface IContextRoutingService
{
    /// <summary>
    /// 根据依赖与可见性策略路由上下文工作包。
    /// </summary>
    Task RouteAsync(
        SwarmExecutionPlan plan,
        CancellationToken cancellationToken = default);
}
