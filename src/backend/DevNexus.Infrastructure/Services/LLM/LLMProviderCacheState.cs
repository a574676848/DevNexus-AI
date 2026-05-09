using System.Threading;

namespace DevNexus.Infrastructure.Services.LLM;

/// <summary>
/// Tracks provider configuration changes to invalidate kernel caches.
/// </summary>
public class LLMProviderCacheState
{
    private long _version;

    public long Version => Interlocked.Read(ref _version);

    public long Increment()
    {
        return Interlocked.Increment(ref _version);
    }
}
