using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using DevNexus.Core.Services.Swarm.Analysis;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;

namespace DevNexus.Core.Services.Swarm.Generation;

/// <summary>
/// 智能体工厂接口
/// </summary>
public interface IAgentFactory
{
    /// <summary>
    /// 创建动态智能体
    /// </summary>
    /// <param name="taskDescription">任务描述</param>
    /// <param name="domain">领域</param>
    /// <param name="availableTools">可用工具</param>
    /// <param name="providerId">LLM Provider ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>Agent 实例</returns>
    Task<ChatCompletionAgent> CreateAgentAsync(
        string taskDescription, 
        DomainType domain,
        List<string> availableTools,
        Guid providerId,
        string sessionId,
        Context.IBlackboard? blackboard = null,
        string? taskId = null,
        CancellationToken cancellationToken = default);
}
