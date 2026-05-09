using System;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace DevNexus.Core.Services.Swarm.Planning;

/// <summary>
/// 自适应并发控制器 - 基于吞吐量动态调整并发数
/// </summary>
public class AdaptiveConcurrencyController : IDisposable
{
    private readonly ILogger<AdaptiveConcurrencyController> _logger;
    private readonly ConcurrentQueue<DateTime> _recentCompletions = new();
    private readonly Timer _adjustmentTimer;
    private int _currentConcurrency;
    private readonly object _lock = new();

    // 可以根据配置抽取
    private readonly int _minConcurrency = 2;
    private readonly int _maxConcurrency = 10;
    
    public AdaptiveConcurrencyController(ILogger<AdaptiveConcurrencyController> logger)
    {
        _logger = logger;
        _currentConcurrency = _minConcurrency;
        
        // 每 30 秒调整一次
        _adjustmentTimer = new Timer(AdjustConcurrency, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }
    
    public int CurrentConcurrency
    {
        get
        {
            lock (_lock)
            {
                return _currentConcurrency;
            }
        }
    }
    
    public void RecordTaskCompletion()
    {
        _recentCompletions.Enqueue(DateTime.UtcNow);
    }
    
    private void AdjustConcurrency(object? state)
    {
        CleanupOldCompletions();
        
        var throughput = _recentCompletions.Count; // 最近 30 秒完成的任务数
        var targetThroughput = _maxConcurrency * 2; // 目标估算
        
        lock (_lock)
        {
            var previous = _currentConcurrency;
            
            if (throughput > targetThroughput && _currentConcurrency < _maxConcurrency)
            {
                _currentConcurrency++;
                _logger.LogInformation("[自适应并发] 扩容: {Previous} -> {Current} (30秒完成: {Throughput})", 
                    previous, _currentConcurrency, throughput);
            }
            else if (throughput < targetThroughput / 2 && _currentConcurrency > _minConcurrency)
            {
                _currentConcurrency--;
                _logger.LogInformation("[自适应并发] 缩容: {Previous} -> {Current} (30秒完成: {Throughput})", 
                    previous, _currentConcurrency, throughput);
            }
        }
    }
    
    private void CleanupOldCompletions()
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-30);
        while (_recentCompletions.TryPeek(out var timestamp) && timestamp < cutoff)
        {
            _recentCompletions.TryDequeue(out _);
        }
    }
    
    public void Dispose()
    {
        _adjustmentTimer.Dispose();
    }
}