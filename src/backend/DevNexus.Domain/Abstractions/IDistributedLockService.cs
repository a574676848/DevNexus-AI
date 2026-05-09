namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 分布式锁服务接口
/// </summary>
public interface IDistributedLockService
{
    /// <summary>
    /// 尝试获取分布式锁
    /// </summary>
    /// <param name="lockKey">锁的键</param>
    /// <param name="expiryTime">锁的过期时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>锁的唯一标识符，如果获取失败则返回null</returns>
    Task<string?> TryAcquireLockAsync(string lockKey, TimeSpan expiryTime, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 释放分布式锁
    /// </summary>
    /// <param name="lockKey">锁的键</param>
    /// <param name="lockValue">锁的唯一标识符</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功释放</returns>
    Task<bool> ReleaseLockAsync(string lockKey, string lockValue, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 续期锁的过期时间
    /// </summary>
    /// <param name="lockKey">锁的键</param>
    /// <param name="lockValue">锁的唯一标识符</param>
    /// <param name="expiryTime">新的过期时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功续期</returns>
    Task<bool> RenewLockAsync(string lockKey, string lockValue, TimeSpan expiryTime, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 执行带锁的操作
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    /// <param name="lockKey">锁的键</param>
    /// <param name="action">要执行的操作</param>
    /// <param name="expiryTime">锁的过期时间</param>
    /// <param name="waitTime">等待获取锁的最大时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<T?> ExecuteWithLockAsync<T>(
        string lockKey,
        Func<CancellationToken, Task<T>> action,
        TimeSpan expiryTime,
        TimeSpan? waitTime = null,
        CancellationToken cancellationToken = default);
}
