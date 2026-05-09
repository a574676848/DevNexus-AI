using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevNexus.Domain.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;

namespace DevNexus.Core.Services.Swarm.Memory;

/// <summary>
/// 分层记忆服务实现
/// L1: 本地 ConcurrentDictionary 缓存
/// L2: 基于 SessionRepository 的最近上下文内容
/// L3: 基于 IKernelMemory (若启用) 的向量长期检索
/// </summary>
public class TieredMemoryService : ITieredMemoryService
{
    // L1: 简单的进程内缓存
    private static readonly ConcurrentDictionary<string, List<MemoryEntry>> _l1Cache = new();
    
    // 我们现有的基础设施
    private readonly IContextSwarmSessionRepository _sessionRepository;
    private readonly IKernelMemory _kernelMemory; 
    private readonly ILogger<TieredMemoryService> _logger;

    public TieredMemoryService(
        IContextSwarmSessionRepository sessionRepository,
        IKernelMemory kernelMemory,
        ILogger<TieredMemoryService> logger)
    {
        _sessionRepository = sessionRepository;
        _kernelMemory = kernelMemory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StoreAsync(
        string sessionId, 
        string key, 
        string content, 
        IDictionary<string, string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Storing memory to Tiered System. Session={Session}, Key={Key}", sessionId, key);

        var entry = new MemoryEntry 
        { 
            Key = key, 
            Content = content, 
            Metadata = tags ?? new Dictionary<string, string>(),
            Timestamp = DateTime.UtcNow 
        };

        // 1. 存入 L1 (最近使用)
        var sessionEntries = _l1Cache.GetOrAdd(sessionId, _ => new List<MemoryEntry>());
        lock (sessionEntries)
        {
            sessionEntries.Insert(0, entry);
            if (sessionEntries.Count > 20) sessionEntries.RemoveAt(sessionEntries.Count - 1); // 维持 L1 大小
        }

        // 2. 存入 L3 (向量数据库) 进行长期检索
        try
        {
            var tagCollection = new TagCollection();
            if (tags != null)
            {
                foreach (var tag in tags) tagCollection.Add(tag.Key, tag.Value);
            }
            tagCollection.Add("sessionId", sessionId);
            tagCollection.Add("source", "swarm-memory");

            await _kernelMemory.ImportTextAsync(content, sessionId + "_" + key, tags: tagCollection, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store memory to L3 (Kernel Memory).");
        }
    }

    /// <inheritdoc />
    public async Task<List<string>> RecallAsync(
        string sessionId, 
        string query, 
        int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Recalling memory for Query: {Query}", query);
        
        var results = new List<string>();

        // 1. 从 L1 检索 (基于关键词匹配的简单过滤)
        if (_l1Cache.TryGetValue(sessionId, out var entries))
        {
            var l1Matches = entries
                .Where(e => e.Content.Contains(query, StringComparison.OrdinalIgnoreCase) || 
                            e.Key.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .Select(e => $"[L1 Memory] {e.Content}")
                .ToList();
            results.AddRange(l1Matches);
        }

        if (results.Count >= maxResults) return results;

        // 2. 从 L3 检索 (向量相似度)
        try
        {
            var searchResult = await _kernelMemory.SearchAsync(query, index: null, filters: new List<MemoryFilter> 
            {
                MemoryFilters.ByTag("sessionId", sessionId)
            }, limit: maxResults - results.Count, cancellationToken: cancellationToken);

            results.AddRange(searchResult.Results.SelectMany(r => r.Partitions).Select(p => $"[L3 Long-Term] {p.Text}"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to recall from L3 (Kernel Memory).");
        }

        return results.Distinct().ToList();
    }

    /// <inheritdoc />
    public async Task ClearSessionMemoryAsync(string sessionId)
    {
        _l1Cache.TryRemove(sessionId, out _);
        await Task.CompletedTask;
    }

    private class MemoryEntry
    {
        public string Key { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
        public DateTime Timestamp { get; set; }
    }
}
