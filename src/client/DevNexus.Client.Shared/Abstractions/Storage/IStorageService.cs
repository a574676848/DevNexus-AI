namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// 本地存储服务接口 - 提供 SQLite 离线缓存能力
/// </summary>
public interface IStorageService
{
    Task InitializeAsync();
    Task SaveSessionsAsync(IEnumerable<DevNexus.Shared.DTOs.ChatSessionDto> sessions);
    Task<List<DevNexus.Shared.DTOs.ChatSessionDto>> LoadSessionsAsync();
    Task SaveMessagesAsync(Guid sessionId, IEnumerable<DevNexus.Shared.DTOs.ChatMessageDto> messages);
    Task<List<DevNexus.Shared.DTOs.ChatMessageDto>> LoadMessagesAsync(Guid sessionId);
    Task DeleteSessionAsync(Guid sessionId);
    Task ClearCacheAsync();
}

