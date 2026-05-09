using System;
using System.Threading;
using System.Threading.Tasks;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// 会话临时记忆服务接口 (P5)
/// </summary>
public interface ISessionMemoryService
{
    /// <summary>
    /// 保存或更新会话记忆
    /// </summary>
    Task SaveAsync(string userId, string sessionId, string name, string category, 
        string summary, string content, CancellationToken ct = default);
    
    /// <summary>
    /// 读取会话记忆详情
    /// </summary>
    Task<string?> ReadAsync(string userId, string sessionId, string name, CancellationToken ct = default);
    
    /// <summary>
    /// 获取记忆目录（_index.md 内容）
    /// </summary>
    Task<string> GetIndexAsync(string userId, string sessionId, CancellationToken ct = default);
    
    /// <summary>
    /// 删除会话记忆
    /// </summary>
    Task DeleteAsync(string userId, string sessionId, string name, CancellationToken ct = default);
    
    /// <summary>
    /// 删除整个会话的所有记忆
    /// </summary>
    Task DeleteAllAsync(string userId, string sessionId, CancellationToken ct = default);
    
}
