// using DevNexus.Domain.Abstractions via GlobalUsings
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DevNexus.Infrastructure.Services.Cache;

/// <summary>
/// 上下文缓存服务实现
/// 基于 Redis 缓存活跃会话的最近20条消息，减少数据库 IO，优化首字延迟
/// </summary>
public class ContextCacheService : IContextCacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<ContextCacheService> _logger;
    private const int MaxCachedMessages = 20;
    private const int CacheExpirationMinutes = 30;
    
    public ContextCacheService(
        IDistributedCache cache,
        ILogger<ContextCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }
    
    /// <inheritdoc />
    public async Task<List<ChatMessageDto>?> GetSessionContextAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(sessionId);
        
        try
        {
            var cachedData = await _cache.GetStringAsync(cacheKey, cancellationToken);
            
            if (string.IsNullOrEmpty(cachedData))
            {
                _logger.LogDebug("[ContextCache.Get] Cache miss | SessionId={SessionId}", sessionId);
                return null;
            }
            
            var messages = JsonSerializer.Deserialize<List<ChatMessageDto>>(cachedData);
            _logger.LogInformation(
                "[ContextCache.Get] Cache hit | SessionId={SessionId} MessageCount={Count}",
                sessionId,
                messages?.Count ?? 0);
            
            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[ContextCache.Get] Failed to retrieve cache | SessionId={SessionId}",
                sessionId);
            return null;
        }
    }
    
    /// <inheritdoc />
    public async Task UpdateSessionContextAsync(
        Guid sessionId,
        List<ChatMessageDto> messages,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(sessionId);
        
        try
        {
            // 只缓存最近的消息
            var messagesToCache = messages
                .OrderByDescending(m => m.CreatedAt)
                .Take(MaxCachedMessages)
                .OrderBy(m => m.CreatedAt)
                .ToList();
            
            var serializedData = JsonSerializer.Serialize(messagesToCache);
            
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheExpirationMinutes)
            };
            
            await _cache.SetStringAsync(cacheKey, serializedData, options, cancellationToken);
            
            _logger.LogInformation(
                "[ContextCache.Update] Cache updated | SessionId={SessionId} MessageCount={Count} ExpiresIn={Minutes}m",
                sessionId,
                messagesToCache.Count,
                CacheExpirationMinutes);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[ContextCache.Update] Failed to update cache | SessionId={SessionId}",
                sessionId);
        }
    }
    
    /// <inheritdoc />
    public async Task AppendMessageAsync(
        Guid sessionId,
        ChatMessageDto message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cachedMessages = await GetSessionContextAsync(sessionId, cancellationToken);
            
            if (cachedMessages == null)
            {
                // 缓存不存在，创建新缓存
                cachedMessages = new List<ChatMessageDto> { message };
            }
            else
            {
                // 追加新消息
                cachedMessages.Add(message);
                
                // 如果超过最大数量，移除最旧的消息
                if (cachedMessages.Count > MaxCachedMessages)
                {
                    cachedMessages = cachedMessages
                        .OrderByDescending(m => m.CreatedAt)
                        .Take(MaxCachedMessages)
                        .OrderBy(m => m.CreatedAt)
                        .ToList();
                }
            }
            
            await UpdateSessionContextAsync(sessionId, cachedMessages, cancellationToken);
            
            _logger.LogDebug(
                "[ContextCache.Append] Message appended | SessionId={SessionId} MessageId={MessageId}",
                sessionId,
                message.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[ContextCache.Append] Failed to append message | SessionId={SessionId}",
                sessionId);
        }
    }
    
    /// <inheritdoc />
    public async Task ClearSessionContextAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(sessionId);
        
        try
        {
            await _cache.RemoveAsync(cacheKey, cancellationToken);
            
            _logger.LogInformation(
                "[ContextCache.Clear] Cache cleared | SessionId={SessionId}",
                sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[ContextCache.Clear] Failed to clear cache | SessionId={SessionId}",
                sessionId);
        }
    }
    
    private static string GetCacheKey(Guid sessionId) => $"session:{sessionId}:context";
}
