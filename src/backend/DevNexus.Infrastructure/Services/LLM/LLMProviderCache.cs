using System.Collections.Concurrent;

namespace DevNexus.Infrastructure.Services.LLM;

/// <summary>
/// LLM 提供商实例缓存 (Singleton)
/// 用于跨请求复用 ILLMProvider 实例，避免重复创建 HttpClient 和 ChatCompletionService
/// </summary>
public class LLMProviderCache
{
    // Key: LLMProvider.Id (数据库主键) -> Value: ILLMProvider instance
    private readonly ConcurrentDictionary<Guid, ILLMProvider> _cache = new();

    public ILLMProvider? Get(Guid id)
    {
        if (_cache.TryGetValue(id, out var provider))
        {
            return provider;
        }
        return null;
    }

    public void Set(Guid id, ILLMProvider provider)
    {
        _cache[id] = provider;
    }

    public void Remove(Guid id)
    {
        _cache.TryRemove(id, out _);
    }
    
    public void Clear()
    {
        _cache.Clear();
    }
}
