using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Plugins;

/// <summary>
/// Shared helper for plugins that need a per-session user context.
/// </summary>
public abstract class SessionContextPluginBase
{
    private readonly ConcurrentDictionary<Guid, Guid> _sessionUsers = new();
    private readonly ILogger _logger;

    protected SessionContextPluginBase(ILogger logger)
    {
        _logger = logger;
    }

    protected void SetSessionContext(Guid sessionId, Guid userId, string source)
    {
        _sessionUsers[sessionId] = userId;
        _logger.LogDebug("[{Source}] Context set | SessionId={SessionId} UserId={UserId}", source, sessionId, userId);
    }

    protected Guid GetSessionUserId(Guid? sessionId = null)
    {
        if (sessionId.HasValue && _sessionUsers.TryGetValue(sessionId.Value, out var userId))
        {
            return userId;
        }

        if (_sessionUsers.Any())
        {
            return _sessionUsers.First().Value;
        }

        return Guid.Empty;
    }
}
