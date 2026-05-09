using DevNexus.Domain.Models.Swarm;
using DevNexus.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace DevNexus.Core.Services.Swarm.Routing;

/// <summary>
/// 默认上下文路由服务。
/// </summary>
public class DefaultContextRoutingService : IContextRoutingService
{
    private readonly ILogger<DefaultContextRoutingService> _logger;

    /// <summary>
    /// 初始化上下文路由服务。
    /// </summary>
    public DefaultContextRoutingService(ILogger<DefaultContextRoutingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 根据当前计划记录路由动作。
    /// </summary>
    public Task RouteAsync(
        SwarmExecutionPlan plan,
        CancellationToken cancellationToken = default)
    {
        foreach (var package in plan.Packages)
        {
            package.MemoryContext = TrimContext(package.MemoryContext, package.VisibilityLevel);
            package.EvidenceContext = TrimContext(package.EvidenceContext, package.VisibilityLevel);
            package.StateContext = TrimContext(package.StateContext, package.VisibilityLevel);
        }

        _logger.LogInformation(
            "Swarm 上下文路由完成 | SessionId={SessionId} PackageCount={PackageCount}",
            plan.SessionId,
            plan.Packages.Count);

        return Task.CompletedTask;
    }

    private static string TrimContext(string content, SwarmVisibilityLevel visibilityLevel)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var maxLength = visibilityLevel switch
        {
            SwarmVisibilityLevel.PackageOnly => 240,
            SwarmVisibilityLevel.DependencyScoped => 480,
            _ => 960
        };

        if (content.Length <= maxLength)
        {
            return content;
        }

        return content[..maxLength] + " ...[已按可见范围裁剪]";
    }
}
