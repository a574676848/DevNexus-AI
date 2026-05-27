using System.Collections.Concurrent;

namespace DevNexus.Core.Services.Chat;

internal sealed class ChatGenerationCancellationRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _sources = new();

    internal int Count => _sources.Count;

    public bool TryRegister(Guid sessionId, CancellationTokenSource cts)
    {
        ArgumentNullException.ThrowIfNull(cts);
        return _sources.TryAdd(sessionId, cts);
    }

    public bool Cancel(Guid sessionId)
    {
        if (!_sources.TryRemove(sessionId, out var cts))
        {
            return false;
        }

        cts.Cancel();
        return true;
    }

    public bool Complete(Guid sessionId, CancellationTokenSource cts)
    {
        ArgumentNullException.ThrowIfNull(cts);
        var entry = new KeyValuePair<Guid, CancellationTokenSource>(sessionId, cts);
        return ((ICollection<KeyValuePair<Guid, CancellationTokenSource>>)_sources).Remove(entry);
    }
}
