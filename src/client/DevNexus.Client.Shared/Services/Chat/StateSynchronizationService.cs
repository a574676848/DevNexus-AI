using DevNexus.Client.Shared.Abstractions;
using DevNexus.Client.Shared.Abstractions.Chat;
using DevNexus.Client.Shared.Models;
using Microsoft.Extensions.Logging;

namespace DevNexus.Client.Shared.Services.Chat;

/// <summary>
/// 状态同步服务实现 - 处理状态变化事件和UI更新
/// </summary>
public class StateSynchronizationService : IStateSynchronizationService
{
    private readonly IChatState _chatState;
    private readonly ISessionState _sessionState;
    private readonly ILogger<StateSynchronizationService> _logger;

    public StateSynchronizationService(
        IChatState chatState,
        ISessionState sessionState,
        ILogger<StateSynchronizationService> logger)
    {
        _chatState = chatState;
        _sessionState = sessionState;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool HasOtherGeneratingSession(Guid currentSessionId)
    {
        var generatingSessions = _sessionState.Sessions
            .Select(session => session.Id)
            .Where(id => _chatState.GetSessionRunControl(id).IsGenerationLike);
        return generatingSessions.Any(id => id != currentSessionId);
    }

    /// <inheritdoc />
    public Guid? GetGeneratingSessionId(Guid excludeSessionId)
    {
        var generatingSession = _sessionState.Sessions
            .Select(session => session.Id)
            .Where(id => _chatState.GetSessionRunControl(id).IsGenerationLike)
            .FirstOrDefault(id => id != excludeSessionId);

        return generatingSession != Guid.Empty ? generatingSession : null;
    }
}
