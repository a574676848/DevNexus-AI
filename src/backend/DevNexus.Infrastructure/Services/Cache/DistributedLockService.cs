// using DevNexus.Domain.Abstractions via GlobalUsings
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace DevNexus.Infrastructure.Services.Cache;

/// <summary>
/// 基于 Redis 的分布式锁服务实现
/// 支持自动续期和超时释放，适用于 Wiki 生成、OSS 上传等全局任务
/// </summary>
public class DistributedLockService : IDistributedLockService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<DistributedLockService> _logger;
    
    public DistributedLockService(
        IConnectionMultiplexer redis,
        ILogger<DistributedLockService> logger)
    {
        _redis = redis;
        _logger = logger;
    }
    
    /// <inheritdoc />
    public async Task<string?> TryAcquireLockAsync(
        string lockKey,
        TimeSpan expiryTime,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var lockValue = Guid.NewGuid().ToString();
        var fullLockKey = GetLockKey(lockKey);
        
        try
        {
            var acquired = await db.StringSetAsync(
                fullLockKey,
                lockValue,
                expiryTime,
                When.NotExists);
            
            if (acquired)
            {
                _logger.LogInformation(
                    "[DistributedLock.Acquire] Lock acquired | Key={LockKey} Value={LockValue} ExpiresIn={Expiry}",
                    lockKey,
                    lockValue,
                    expiryTime);
                return lockValue;
            }
            
            _logger.LogDebug(
                "[DistributedLock.Acquire] Lock already held | Key={LockKey}",
                lockKey);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[DistributedLock.Acquire] Failed to acquire lock | Key={LockKey}",
                lockKey);
            return null;
        }
    }
    
    /// <inheritdoc />
    public async Task<bool> ReleaseLockAsync(
        string lockKey,
        string lockValue,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var fullLockKey = GetLockKey(lockKey);
        
        try
        {
            // 使用 Lua 脚本确保只有锁的持有者才能释放锁
            var script = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end";
            
            var result = await db.ScriptEvaluateAsync(
                script,
                new RedisKey[] { fullLockKey },
                new RedisValue[] { lockValue });
            
            var released = (int)result == 1;
            
            if (released)
            {
                _logger.LogInformation(
                    "[DistributedLock.Release] Lock released | Key={LockKey} Value={LockValue}",
                    lockKey,
                    lockValue);
            }
            else
            {
                _logger.LogWarning(
                    "[DistributedLock.Release] Lock not owned or already released | Key={LockKey}",
                    lockKey);
            }
            
            return released;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[DistributedLock.Release] Failed to release lock | Key={LockKey}",
                lockKey);
            return false;
        }
    }
    
    /// <inheritdoc />
    public async Task<bool> RenewLockAsync(
        string lockKey,
        string lockValue,
        TimeSpan expiryTime,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var fullLockKey = GetLockKey(lockKey);
        
        try
        {
            // 使用 Lua 脚本确保只有锁的持有者才能续期
            var script = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('pexpire', KEYS[1], ARGV[2])
                else
                    return 0
                end";
            
            var result = await db.ScriptEvaluateAsync(
                script,
                new RedisKey[] { fullLockKey },
                new RedisValue[] { lockValue, (long)expiryTime.TotalMilliseconds });
            
            var renewed = (int)result == 1;
            
            if (renewed)
            {
                _logger.LogDebug(
                    "[DistributedLock.Renew] Lock renewed | Key={LockKey} ExpiresIn={Expiry}",
                    lockKey,
                    expiryTime);
            }
            else
            {
                _logger.LogWarning(
                    "[DistributedLock.Renew] Lock not owned or expired | Key={LockKey}",
                    lockKey);
            }
            
            return renewed;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[DistributedLock.Renew] Failed to renew lock | Key={LockKey}",
                lockKey);
            return false;
        }
    }
    
    /// <inheritdoc />
    public async Task<T?> ExecuteWithLockAsync<T>(
        string lockKey,
        Func<CancellationToken, Task<T>> action,
        TimeSpan expiryTime,
        TimeSpan? waitTime = null,
        CancellationToken cancellationToken = default)
    {
        var maxWaitTime = waitTime ?? TimeSpan.FromSeconds(10);
        var retryDelay = TimeSpan.FromMilliseconds(100);
        var startTime = DateTime.UtcNow;
        
        string? lockValue = null;
        
        try
        {
            // 尝试获取锁，如果失败则重试
            while (lockValue == null && DateTime.UtcNow - startTime < maxWaitTime)
            {
                lockValue = await TryAcquireLockAsync(lockKey, expiryTime, cancellationToken);
                
                if (lockValue == null)
                {
                    await Task.Delay(retryDelay, cancellationToken);
                }
            }
            
            if (lockValue == null)
            {
                _logger.LogWarning(
                    "[DistributedLock.Execute] Failed to acquire lock within timeout | Key={LockKey} Timeout={Timeout}",
                    lockKey,
                    maxWaitTime);
                return default;
            }
            
            // 执行操作
            _logger.LogDebug(
                "[DistributedLock.Execute] Executing action with lock | Key={LockKey}",
                lockKey);
            
            var result = await action(cancellationToken);
            
            _logger.LogInformation(
                "[DistributedLock.Execute] Action completed | Key={LockKey}",
                lockKey);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[DistributedLock.Execute] Action failed | Key={LockKey}",
                lockKey);
            throw;
        }
        finally
        {
            // 释放锁
            if (lockValue != null)
            {
                await ReleaseLockAsync(lockKey, lockValue, cancellationToken);
            }
        }
    }
    
    private static string GetLockKey(string key) => $"lock:{key}";
}
