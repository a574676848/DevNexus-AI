using System.Collections.Concurrent;

namespace DevNexus.Client.Shared.Services.Communication;

internal sealed class PendingGenerationCancelQueue
{
    private readonly ConcurrentDictionary<Guid, byte> _sessionIds = new();

    internal int Count => _sessionIds.Count;

    public void Enqueue(Guid sessionId)
    {
        if (sessionId == Guid.Empty)
        {
            return;
        }

        _sessionIds.TryAdd(sessionId, 0);
    }

    public IReadOnlyList<Guid> Drain()
    {
        if (_sessionIds.IsEmpty)
        {
            return Array.Empty<Guid>();
        }

        var sessionIds = _sessionIds.Keys.ToList();
        foreach (var sessionId in sessionIds)
        {
            _sessionIds.TryRemove(sessionId, out _);
        }

        return sessionIds;
    }
}
