using DevNexus.Shared.DTOs;
using DevNexus.Client.Shared.Abstractions;

namespace DevNexus.Client.Shared.Services.State;

/// <summary>
/// 会话状态管理实现
/// </summary>
public class SessionState : ISessionState
{
    private readonly List<ChatSessionDto> _sessions = new();
    private ChatSessionDto? _currentSession;
    private bool _isLoading;

    /// <inheritdoc />
    public event Action? OnStateChanged;

    /// <inheritdoc />
    public IReadOnlyList<ChatSessionDto> Sessions => _sessions.AsReadOnly();

    /// <inheritdoc />
    public ChatSessionDto? CurrentSession => _currentSession;

    /// <inheritdoc />
    public bool IsLoading => _isLoading;

    /// <inheritdoc />
    public void SetSessions(IEnumerable<ChatSessionDto> sessions)
    {
        _sessions.Clear();
        _sessions.AddRange(sessions);

        // 如果当前会话不在列表中，清除当前会话
        if (_currentSession != null && !_sessions.Any(s => s.Id == _currentSession.Id))
        {
            _currentSession = null;
        }

        NotifyStateChanged();
    }

    /// <inheritdoc />
    public void AddSession(ChatSessionDto session)
    {
        _sessions.Insert(0, session); // 新会话添加到顶部
        NotifyStateChanged();
    }

    /// <inheritdoc />
    public void UpdateSession(ChatSessionDto session)
    {
        var index = _sessions.FindIndex(s => s.Id == session.Id);
        if (index >= 0)
        {
            _sessions[index] = session;

            if (_currentSession?.Id == session.Id)
            {
                _currentSession = session;
            }

            NotifyStateChanged();
        }
    }

    /// <inheritdoc />
    public void RemoveSession(Guid sessionId)
    {
        var removed = _sessions.RemoveAll(s => s.Id == sessionId);
        if (removed > 0)
        {
            if (_currentSession?.Id == sessionId)
            {
                _currentSession = _sessions.FirstOrDefault();
            }
            NotifyStateChanged();
        }
    }

    /// <inheritdoc />
    public void SetCurrentSession(Guid sessionId)
    {
        var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
        if (session != null && _currentSession?.Id != sessionId)
        {
            _currentSession = session;
            NotifyStateChanged();
        }
    }

    /// <inheritdoc />
    public void SetLoading(bool loading)
    {
        if (_isLoading != loading)
        {
            _isLoading = loading;
            NotifyStateChanged();
        }
    }

    /// <inheritdoc />
    public void Clear()
    {
        _sessions.Clear();
        _currentSession = null;
        _isLoading = false;
        NotifyStateChanged();
    }

    /// <summary>
    /// 触发状态变更通知
    /// </summary>
    private void NotifyStateChanged() => OnStateChanged?.Invoke();
}
