using System.Text.Json;
using DevNexus.Shared.DTOs;
using SQLite;

namespace DevNexus.Client.Services.Storage;

/// <summary>
/// SQLite 本地存储服务实现
/// </summary>
public class SqliteStorageService : IStorageService
{
    private SQLiteAsyncConnection? _database;
    private readonly string _dbPath;

    public SqliteStorageService()
    {
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "devnexus_cache.db");
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        if (_database != null) return;

        _database = new SQLiteAsyncConnection(_dbPath);
        await _database.CreateTableAsync<SessionCache>();
        await _database.CreateTableAsync<MessageCache>();
    }

    /// <inheritdoc />
    public async Task SaveSessionsAsync(IEnumerable<ChatSessionDto> sessions)
    {
        await InitializeAsync();

        foreach (var session in sessions)
        {
            var cache = new SessionCache
            {
                Id = session.Id,
                Title = session.Title ?? "新会话",
                UpdatedAt = session.UpdatedAt,
                JsonData = JsonSerializer.Serialize(session)
            };

            await _database!.InsertOrReplaceAsync(cache);
        }
    }

    /// <inheritdoc />
    public async Task<List<ChatSessionDto>> LoadSessionsAsync()
    {
        await InitializeAsync();

        var caches = await _database!.Table<SessionCache>()
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync();

        return caches
            .Select(c => JsonSerializer.Deserialize<ChatSessionDto>(c.JsonData))
            .Where(s => s != null)
            .Cast<ChatSessionDto>()
            .ToList();
    }

    /// <inheritdoc />
    public async Task SaveMessagesAsync(Guid sessionId, IEnumerable<ChatMessageDto> messages)
    {
        await InitializeAsync();

        // 先删除该会话的旧消息
        await _database!.ExecuteAsync($"DELETE FROM {nameof(MessageCache)} WHERE SessionId = ?", sessionId);

        foreach (var message in messages)
        {
            var cache = new MessageCache
            {
                Id = message.Id,
                SessionId = sessionId,
                JsonData = JsonSerializer.Serialize(message)
            };

            await _database.InsertAsync(cache);
        }
    }

    /// <inheritdoc />
    public async Task<List<ChatMessageDto>> LoadMessagesAsync(Guid sessionId)
    {
        await InitializeAsync();

        var caches = await _database!.Table<MessageCache>()
            .Where(m => m.SessionId == sessionId)
            .ToListAsync();

        return caches
            .Select(c => JsonSerializer.Deserialize<ChatMessageDto>(c.JsonData))
            .Where(m => m != null)
            .Cast<ChatMessageDto>()
            .ToList();
    }

    /// <inheritdoc />
    public async Task DeleteSessionAsync(Guid sessionId)
    {
        await InitializeAsync();

        await _database!.ExecuteAsync($"DELETE FROM {nameof(MessageCache)} WHERE SessionId = ?", sessionId);
        await _database.DeleteAsync<SessionCache>(sessionId);
    }

    /// <inheritdoc />
    public async Task ClearCacheAsync()
    {
        await InitializeAsync();

        await _database!.DeleteAllAsync<MessageCache>();
        await _database.DeleteAllAsync<SessionCache>();
    }
}

/// <summary>
/// 会话缓存表
/// </summary>
[Table("sessions")]
public class SessionCache
{
    [PrimaryKey]
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }

    public string JsonData { get; set; } = string.Empty;
}

/// <summary>
/// 消息缓存表
/// </summary>
[Table("messages")]
public class MessageCache
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid SessionId { get; set; }

    public string JsonData { get; set; } = string.Empty;
}

