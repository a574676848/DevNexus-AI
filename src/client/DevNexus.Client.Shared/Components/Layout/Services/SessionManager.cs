using DevNexus.Client.Shared.Services.State;
using DevNexus.Client.Shared.Models;
using DevNexus.Shared.DTOs;
using Microsoft.AspNetCore.Components;

namespace DevNexus.Client.Shared.Components.Layout.Services;

/// <summary>
/// 会话管理服务 - 负责会话的创建、删除、重命名、固定等操作
/// </summary>
public class SessionManager
{
    private readonly IApiService _apiService;
    private readonly ISessionState _sessionState;
    private readonly IChatState _chatState;
    private readonly NavigationManager _navigationManager;
    private readonly IRemoteLogService _remoteLogService;
    private readonly HashSet<Guid> _pinnedSessionIds = new();
    private const string BlankSessionTitle = "新会话";

    public event Action? OnSessionsChanged;

    public SessionManager(
        IApiService apiService,
        ISessionState sessionState,
        IChatState chatState,
        NavigationManager navigationManager,
        IRemoteLogService remoteLogService)
    {
        _apiService = apiService;
        _sessionState = sessionState;
        _chatState = chatState;
        _navigationManager = navigationManager;
        _remoteLogService = remoteLogService;
    }

    public IEnumerable<Guid> PinnedSessionIds => _pinnedSessionIds;

    /// <summary>
    /// 创建或进入空白会话。
    /// </summary>
    public async Task CreateOrEnterBlankSessionAsync()
    {
        var blankSession = FindExistingBlankSession();

        if (blankSession != null)
        {
            EnterSession(blankSession.Id);
            return;
        }

        try
        {
            _sessionState.SetLoading(true);

            var session = await _apiService.CreateSessionAsync(BlankSessionTitle);
            if (session != null)
            {
                _sessionState.AddSession(session);
                EnterSession(session.Id);
            }

            OnSessionsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"创建空白会话失败: {ex.Message}");
            _ = _remoteLogService.LogErrorAsync(ex, "Layout.SessionManager.CreateOrEnterBlankSessionAsync");
            OnSessionsChanged?.Invoke();
        }
        finally
        {
            _sessionState.SetLoading(false);
        }
    }

    /// <summary>
    /// 进入指定会话
    /// </summary>
    public void EnterSession(Guid sessionId)
    {
        _chatState.SetCurrentSession(sessionId);
        _sessionState.SetCurrentSession(sessionId);

        _navigationManager.NavigateTo($"/chat/{sessionId}");
    }

    /// <summary>
    /// 查找已存在的空白会话
    /// </summary>
    private ChatSessionDto? FindExistingBlankSession()
    {
        return _sessionState.Sessions
            .FirstOrDefault(s => s.Title == BlankSessionTitle && s.MessageCount == 0);
    }

    /// <summary>
    /// 处理固定/取消固定会话
    /// </summary>
    public void TogglePinSession(Guid sessionId)
    {
        if (_pinnedSessionIds.Contains(sessionId))
        {
            _pinnedSessionIds.Remove(sessionId);
        }
        else
        {
            _pinnedSessionIds.Add(sessionId);
        }

        OnSessionsChanged?.Invoke();
    }

    /// <summary>
    /// 重命名会话
    /// </summary>
    public async Task RenameSessionAsync(Guid sessionId, string newTitle)
    {
        try
        {
            var updatedSession = await _apiService.UpdateSessionAsync(
                sessionId,
                new ChatSessionUpdateRequest { Title = newTitle });

            if (updatedSession != null)
            {
                _sessionState.UpdateSession(updatedSession);
            }

            OnSessionsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"重命名会话失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 删除会话
    /// </summary>
    public async Task DeleteSessionAsync(Guid sessionId)
    {
        try
        {
            await _apiService.DeleteSessionAsync(sessionId);
            _sessionState.RemoveSession(sessionId);

            // 清理聊天状态中的会话缓存
            if (_chatState is ChatState cs)
            {
                cs.RemoveSession(sessionId);
            }

            _pinnedSessionIds.Remove(sessionId);

            // 如果删除的是当前会话，进入其他会话
            if (_chatState.CurrentSessionId == sessionId)
            {
                var remainingSessions = _sessionState.Sessions
                    .Where(s => s.Id != sessionId)
                    .OrderByDescending(s => s.UpdatedAt)
                    .ToList();

                if (remainingSessions.Any())
                {
                    EnterSession(remainingSessions.First().Id);
                }
                else
                {
                    _chatState.SetCurrentSession(Guid.Empty);
                    _navigationManager.NavigateTo("/chat");
                }
            }

            OnSessionsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"删除会话失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 获取会话列表项
    /// </summary>
    public List<(Guid Id, string Title, DateTime UpdatedAt, string? LastMessage, int MessageCount, bool IsPinned, SessionRunPresentationState RunPresentation)> GetSessionItems()
    {
        return _sessionState.Sessions
            .Select(s => (
                s.Id,
                s.Title,
                s.UpdatedAt,
                s.LastMessage?.Content,
                s.MessageCount,
                _pinnedSessionIds.Contains(s.Id),
                _chatState.GetSessionRunPresentation(s.Id)))
            .ToList();
    }
}
