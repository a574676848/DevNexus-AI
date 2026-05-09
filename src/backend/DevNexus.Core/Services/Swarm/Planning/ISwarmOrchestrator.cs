using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DevNexus.Core.Services.Swarm.Analysis;
using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Services.Swarm.Planning;

/// <summary>
/// Swarm 主编排服务接口
/// 负责协调复杂度评估、任务分解、模式选择及最终执行
/// </summary>
public interface ISwarmOrchestrator
{
    /// <summary>
    /// 流式编排：接受已评估的复杂度向量和 BlockWriter，支持中间结果实时推送
    /// </summary>
    /// <param name="userRequest">用户请求</param>
    /// <param name="providerId">LLM 供应商 ID</param>
    /// <param name="sessionId">聊天会话 ID，用于关联 Swarm 与发起会话</param>
    /// <param name="complexity">已评估的复杂度向量（避免重复调用 LLM）</param>
    /// <param name="blockWriter">Block 流写入器，用于推送中间进度和最终结果</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>编排执行结果（摘要或最终产物）</returns>
    Task<string> OrchestrateAsync(
        string userRequest,
        Guid providerId,
        string sessionId,
        Guid userId,
        ComplexityVector complexity,
        ChannelWriter<BlockDto> blockWriter,
        CancellationToken cancellationToken = default);
}
