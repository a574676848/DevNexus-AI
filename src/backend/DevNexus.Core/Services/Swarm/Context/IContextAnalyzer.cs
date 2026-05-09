using DevNexus.Core.Services.Swarm.Analysis;
using DevNexus.Domain.Models.Swarm;

namespace DevNexus.Core.Services.Swarm.Context;

/// <summary>
/// 上下文分析服务接口。
/// </summary>
public interface IContextAnalyzer
{
    /// <summary>
    /// 分析用户请求并生成初始上下文工作包草案。
    /// </summary>
    Task<IReadOnlyList<ContextWorkPackage>> AnalyzeAsync(
        string userRequest,
        string sessionId,
        ComplexityVector complexity,
        CancellationToken cancellationToken = default);
}
