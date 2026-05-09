using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DevNexus.Core.Services.Swarm.Memory;

/// <summary>
/// 分层记忆服务接口
/// 实现了 L1 (进程内存) -> L2 (会话数据库) -> L3 (向量长期记忆) 的三层联动
/// </summary>
public interface ITieredMemoryService
{
    /// <summary>
    /// 存储记忆片段到适当的层级
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="key">关键标识</param>
    /// <param name="content">记忆内容</param>
    /// <param name="tags">元数据标签</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task StoreAsync(
        string sessionId, 
        string key, 
        string content, 
        IDictionary<string, string>? tags = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 召回记忆片段
    /// 优先从 L1 检索，最后回退到 L3 向量检索
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="query">检索关键词或描述</param>
    /// <param name="maxResults">最大结果数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>匹配的记忆内容集合</returns>
    Task<List<string>> RecallAsync(
        string sessionId, 
        string query, 
        int maxResults = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 清除特定会话的活跃记忆（L1/L2）
    /// </summary>
    Task ClearSessionMemoryAsync(string sessionId);
}
