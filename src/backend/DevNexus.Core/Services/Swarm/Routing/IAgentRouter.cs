using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevNexus.Core.Services.Swarm.Generation;

namespace DevNexus.Core.Services.Swarm.Routing;

/// <summary>
/// 智能路由接口
/// 负责根据任务描述和上下文，从候选 Agent 列表中选择最匹配的一个或多个 Agent
/// </summary>
public interface IAgentRouter
{
    /// <summary>
    /// 根据意图和候选者选择最优 Agent
    /// </summary>
    /// <param name="taskDescription">任务描述</param>
    /// <param name="candidates">候选 Agent 列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>选中的 Agent Persona</returns>
    Task<AgentPersona?> RouteRequestAsync(
        string taskDescription,
        List<AgentPersona> candidates,
        Guid providerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 意图识别与 Top-K 匹配
    /// </summary>
    Task<List<AgentPersona>> MatchTopKAsync(
        string taskDescription,
        List<AgentPersona> candidates,
        Guid providerId,
        int k = 3,
        CancellationToken cancellationToken = default);
}
