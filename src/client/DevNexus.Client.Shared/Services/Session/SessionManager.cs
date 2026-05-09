using DevNexus.Client.Shared.Abstractions;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;

namespace DevNexus.Client.Shared.Services.Session;

/// <summary>
/// Session 管理器实现 - 协调 API 请求和本地 SQLite 缓存
/// 实现"在线优先、离线回退"策略
/// </summary>
/// <remarks>
/// 策略说明：
/// 1. 优先从 API 加载数据
/// 2. 成功后异步保存到 SQLite 缓存
/// 3. 网络异常时自动从缓存加载
/// </remarks>
public class SessionManager : ISessionManager
{
    private readonly IApiService _apiService;
    private readonly IStorageService _storageService;
    private readonly ISessionState _sessionState;
    private readonly IRemoteLogService _remoteLog;
    private bool _isOfflineMode;

    /// <summary>
    /// 初始化 SessionManager
    /// </summary>
    public SessionManager(
        IApiService apiService,
        IStorageService storageService,
        ISessionState sessionState,
        IRemoteLogService remoteLog)
    {
        _apiService = apiService;
        _storageService = storageService;
        _sessionState = sessionState;
        _remoteLog = remoteLog;
    }

    /// <inheritdoc />
    public bool IsOfflineMode => _isOfflineMode;

    /// <inheritdoc />
    public async Task<List<ChatSessionDto>> LoadSessionsAsync()
    {
        try
        {
            _sessionState.SetLoading(true);

            // 1. 优先从 API 加载
            var sessions = await _apiService.GetSessionsAsync();

            // 标记为在线模式
            _isOfflineMode = false;

            // 2. 更新状态
            _sessionState.SetSessions(sessions);

            // 3. 后台异步缓存到 SQLite（不阻塞主流程）
            _ = PersistSessionsAsync(sessions, "SessionManager.CacheSessions.Failure");

            return sessions;
        }
        catch (HttpRequestException)
        {
            return await LoadSessionsFromCacheAsync();
        }
        catch (Exception ex)
        {
            await _remoteLog.LogErrorAsync(ex, "SessionManager.LoadSessionsAsync");
            return await LoadSessionsFromCacheAsync();
        }
        finally
        {
            _sessionState.SetLoading(false);
        }
    }

    /// <summary>
    /// 从缓存加载会话列表
    /// </summary>
    private async Task<List<ChatSessionDto>> LoadSessionsFromCacheAsync()
    {
        try
        {
            var cached = await _storageService.LoadSessionsAsync();

            // 标记为离线模式
            _isOfflineMode = true;

            _sessionState.SetSessions(cached);
            return cached;
        }
        catch
        {
            return new List<ChatSessionDto>();
        }
    }

    /// <inheritdoc />
    public async Task<List<ChatMessageDto>> LoadMessagesAsync(Guid sessionId)
    {
        try
        {
            // 1. 优先从 API 加载
            var messages = await _apiService.GetMessagesAsync(sessionId);

            // 2. 后台异步缓存
            _ = PersistMessagesAsync(sessionId, messages, "SessionManager.CacheMessages.Failure");

            return messages;
        }
        catch (HttpRequestException)
        {
            return await LoadMessagesFromCacheAsync(sessionId);
        }
        catch (Exception ex)
        {
            await _remoteLog.LogErrorAsync(ex, "SessionManager.LoadMessagesAsync");
            return await LoadMessagesFromCacheAsync(sessionId);
        }
    }

    /// <summary>
    /// 从缓存加载消息
    /// </summary>
    private async Task<List<ChatMessageDto>> LoadMessagesFromCacheAsync(Guid sessionId)
    {
        try
        {
            return await _storageService.LoadMessagesAsync(sessionId);
        }
        catch
        {
            return new List<ChatMessageDto>();
        }
    }

    /// <inheritdoc />
    public async Task CacheSessionsAsync(IEnumerable<ChatSessionDto> sessions)
    {
        try
        {
            await _storageService.SaveSessionsAsync(sessions);
        }
        catch
        {
            // Silently fail manual cache
        }
    }

    /// <inheritdoc />
    public async Task CacheMessagesAsync(Guid sessionId, IEnumerable<ChatMessageDto> messages)
    {
        try
        {
            await _storageService.SaveMessagesAsync(sessionId, messages);
        }
        catch (Exception ex)
        {
            await _remoteLog.LogErrorAsync(ex, "SessionManager.CacheMessages.Failure");
        }
    }

    /// <inheritdoc />
    public async Task DeleteSessionAsync(Guid sessionId)
    {
        try
        {
            // 1. 先清理本地缓存
            await _storageService.DeleteSessionAsync(sessionId);

            // 2. 更新状态
            _sessionState.RemoveSession(sessionId);
        }
        catch (Exception ex)
        {
            await _remoteLog.LogErrorAsync(ex, "SessionManager.DeleteSessionAsync", new Dictionary<string, object?>
            {
                ["SessionId"] = sessionId
            });
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ClearCacheAsync()
    {
        try
        {
            await _storageService.ClearCacheAsync();
        }
        catch (Exception ex)
        {
            await _remoteLog.LogErrorAsync(ex, "SessionManager.ClearCache.Failure");
            throw;
        }
    }
    /// <inheritdoc />
    public async Task<ChatSessionDto?> CreateSessionAsync(string title)
    {
        try
        {
            var session = await _apiService.CreateSessionAsync(title);
            if (session != null)
            {
                // 更新内存状态
                _sessionState.AddSession(session);
                
                // 异步更新缓存
                _ = PersistSessionsAsync(_sessionState.Sessions, "SessionManager.CreateSession.CacheUpdate.Failure");
                return session;
            }
        }
        catch (Exception ex)
        {
            await _remoteLog.LogErrorAsync(ex, "SessionManager.CreateSessionAsync", new Dictionary<string, object?>
            {
                ["Title"] = title
            });
        }
        return null;
    }

    /// <inheritdoc />
    public async Task<ChatSessionDto?> UpdateSessionAsync(Guid sessionId, ChatSessionUpdateRequest request)
    {
        try
        {
            var updatedSession = await _apiService.UpdateSessionAsync(sessionId, request);
            _sessionState.UpdateSession(updatedSession);

            PersistSessionsInBackground("SessionManager.UpdateSession.CacheUpdate.Failure");

            return updatedSession;
        }
        catch (Exception ex)
        {
            await _remoteLog.LogErrorAsync(ex, "SessionManager.UpdateSessionAsync", new Dictionary<string, object?>
            {
                ["SessionId"] = sessionId,
                ["Title"] = request.Title
            });
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateSessionTitleAsync(Guid sessionId, string newTitle)
    {
        await UpdateSessionAsync(sessionId, new ChatSessionUpdateRequest { Title = newTitle });
    }

    /// <inheritdoc />
    public async Task<string?> GenerateSmartTitleAsync(Guid sessionId, string currentTitle)
    {
        try
        {
            // 调用后端 API
            var newTitle = await _apiService.GenerateSmartTitleAsync(sessionId);
            
            if (string.IsNullOrEmpty(newTitle) || newTitle == currentTitle)
            {
                return null;
            }

            // 更新本地状态和缓存
            await UpdateSessionTitleAsync(sessionId, newTitle);
            
            return newTitle;
        }
        catch (Exception ex)
        {
            await _remoteLog.LogErrorAsync(ex, "SessionManager.GenerateSmartTitleAsync");
            return null;
        }
    }

    private void PersistSessionsInBackground(string errorKey)
    {
        _ = PersistSessionsAsync(_sessionState.Sessions, errorKey);
    }

    private async Task PersistSessionsAsync(IEnumerable<ChatSessionDto> sessions, string errorKey)
    {
        try
        {
            await _storageService.SaveSessionsAsync(sessions);
        }
        catch (Exception ex)
        {
            await _remoteLog.LogErrorAsync(ex, errorKey);
        }
    }

    private async Task PersistMessagesAsync(Guid sessionId, IEnumerable<ChatMessageDto> messages, string errorKey)
    {
        try
        {
            await _storageService.SaveMessagesAsync(sessionId, messages);
        }
        catch (Exception ex)
        {
            await _remoteLog.LogErrorAsync(ex, errorKey);
        }
    }
}

