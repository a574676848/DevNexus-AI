using DevNexus.Domain.Models.Swarm;

namespace DevNexus.Core.Services.Swarm.Context;

/// <summary>
/// 上下文切分服务接口。
/// </summary>
public interface IContextSegmenter
{
    /// <summary>
    /// 将上下文草案切分为可独立闭环执行的工作包。
    /// </summary>
    Task<IReadOnlyList<ContextWorkPackage>> SegmentAsync(
        IReadOnlyList<ContextWorkPackage> draftPackages,
        CancellationToken cancellationToken = default);
}
