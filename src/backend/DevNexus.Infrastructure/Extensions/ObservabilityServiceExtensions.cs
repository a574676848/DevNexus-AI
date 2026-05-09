using DevNexus.Core.Abstractions.Observability;
using DevNexus.Infrastructure.Services.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace DevNexus.Infrastructure.Extensions;

/// <summary>
/// 可观测性服务扩展 - 注册分布式追踪和指标收集服务
/// </summary>
public static class ObservabilityServiceExtensions
{
    /// <summary>
    /// 注册可观测性服务到依赖注入容器
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddObservabilityServices(this IServiceCollection services)
    {
        // ⚠️ DistributedTracingService 改为 Singleton：被 AgentLoopMetricsCollector(Singleton) 依赖，且本身是无状态追踪服务
        services.AddSingleton<IDistributedTracingService, SeqTracingService>();

        // 注册 Agent Loop 指标收集器（Singleton 以保持全局状态）
        // 符合洋葱架构：接口定义在 Core.Abstractions，实现在 Infrastructure
        services.AddSingleton<IAgentLoopMetricsCollector, AgentLoopMetricsCollector>();

        return services;
    }
}
