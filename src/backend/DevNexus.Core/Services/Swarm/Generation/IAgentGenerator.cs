using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using DevNexus.Core.Services.Swarm.Analysis;

namespace DevNexus.Core.Services.Swarm.Generation;

/// <summary>
/// 负责基于任务需求动态生成智能体角色
/// </summary>
public interface IAgentGenerator
{
    /// <summary>
    /// 生成智能体人格配置
    /// </summary>
    /// <param name="taskDescription">具体任务描述</param>
    /// <param name="domain">任务所属领域</param>
    /// <param name="availableTools">当前系统可用工具列表</param>
    /// <param name="providerId">LLM 供应商 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>智能体配置</returns>
    Task<AgentPersona> GeneratePersonaAsync(
        string taskDescription, 
        DomainType domain,
        List<string> availableTools,
        Guid providerId,
        CancellationToken cancellationToken = default);
}
