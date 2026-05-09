using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevNexus.Core.Services.Swarm.Analysis;

namespace DevNexus.Core.Services.Swarm.Planning;

/// <summary>
/// 动态工具选择服务，根据任务语义智能选择工具
/// </summary>
public interface IDynamicToolSelector
{
    /// <summary>
    /// 根据任务描述动态选择合适的工具
    /// </summary>
    /// <param name="taskDescription">任务描述</param>
    /// <param name="availableTools">可用工具列表</param>
    /// <param name="providerId">LLM 供应商 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>推荐的工具名称列表</returns>
    Task<List<string>> SelectToolsAsync(
        string taskDescription, 
        List<string> availableTools, 
        Guid providerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据任务类型和领域预测需要的工具
    /// </summary>
    /// <param name="taskType">任务类型（如 "代码生成"、"文件操作"、"网络请求"）</param>
    /// <param name="domain">领域类型</param>
    /// <param name="providerId">LLM 供应商 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>推荐的工具名称列表</returns>
    Task<List<string>> PredictToolsAsync(
        string taskType, 
        DomainType domain, 
        Guid providerId,
        CancellationToken cancellationToken = default);
}
