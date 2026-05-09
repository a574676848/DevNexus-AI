using Microsoft.JSInterop;
using Blazored.LocalStorage;
using DevNexus.Shared.DTOs;
using System.Collections.Concurrent;

namespace DevNexus.Client.Web.Services;

/// <summary>
/// Web 存储服务实现 - 基于 Blazored.LocalStorage（简化内存版本）
/// </summary>
public class WebStorageService : IStorageService
{
    private readonly ILocalStorageService _localStorage;
    private static readonly ConcurrentDictionary<string, List<ChatMessageDto>> _messageCache = new();
    private static List<ChatSessionDto> _sessions = new();

    public WebStorageService(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public Task InitializeAsync()
    {
        return LoadSessionsAsync();
    }

    public async Task SaveSessionsAsync(IEnumerable<ChatSessionDto> sessions)
    {
        _sessions = sessions.ToList();
        await _localStorage.SetItemAsync("chat_sessions", _sessions);
    }

    public async Task<List<ChatSessionDto>> LoadSessionsAsync()
    {
        var sessions = await _localStorage.GetItemAsync<List<ChatSessionDto>>("chat_sessions");
        _sessions = sessions ?? new List<ChatSessionDto>();
        return _sessions;
    }

    public async Task SaveMessagesAsync(Guid sessionId, IEnumerable<ChatMessageDto> messages)
    {
        _messageCache[sessionId.ToString()] = messages.ToList();
        await _localStorage.SetItemAsync($"chat_messages_{sessionId}", messages);
    }

    public async Task<List<ChatMessageDto>> LoadMessagesAsync(Guid sessionId)
    {
        var key = $"chat_messages_{sessionId}";
        if (_messageCache.TryGetValue(sessionId.ToString(), out var cached))
        {
            return cached;
        }

        var messages = await _localStorage.GetItemAsync<List<ChatMessageDto>>(key);
        return messages ?? new List<ChatMessageDto>();
    }

    public async Task DeleteSessionAsync(Guid sessionId)
    {
        _sessions.RemoveAll(s => s.Id == sessionId);
        _messageCache.TryRemove(sessionId.ToString(), out _);
        await _localStorage.SetItemAsync("chat_sessions", _sessions);
        await _localStorage.RemoveItemAsync($"chat_messages_{sessionId}");
    }

    public async Task ClearCacheAsync()
    {
        _sessions.Clear();
        _messageCache.Clear();
        await _localStorage.ClearAsync();
    }
}

