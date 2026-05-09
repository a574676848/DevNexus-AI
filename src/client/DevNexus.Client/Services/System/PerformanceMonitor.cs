using System.Diagnostics;
namespace DevNexus.Client.Services.System;

/// <summary>
/// MAUI 性能监控服务实现
/// 用于监控和优化应用性能
/// </summary>
public class PerformanceMonitor : IPerformanceMonitor
{
    private readonly Dictionary<string, List<double>> _metrics = new();
    private readonly object _lock = new();
    private readonly DateTime _startTime = DateTime.UtcNow;
    private int _totalOperations;

    public PerformanceMonitor()
    {
    }

    /// <inheritdoc />
    public IDisposable StartTrace(string operationName)
    {
        return new PerformanceTrace(this, operationName);
    }

    /// <inheritdoc />
    public void RecordMetric(string name, double value, string unit = "ms")
    {
        lock (_lock)
        {
            if (!_metrics.ContainsKey(name))
            {
                _metrics[name] = new List<double>();
            }

            _metrics[name].Add(value);
            _totalOperations++;

            // 性能警告阈值
            if (value > 1000) // 超过 1 秒
            {
                global::System.Diagnostics.Debug.WriteLine($"[Performance] {name} took {value} {unit}");
            }
        }
    }

    /// <inheritdoc />
    public PerformanceStats GetStats()
    {
        lock (_lock)
        {
            var stats = new PerformanceStats
            {
                TotalOperations = _totalOperations,
                Uptime = DateTime.UtcNow - _startTime,
                MemoryUsageMB = GC.GetTotalMemory(false) / (1024 * 1024)
            };

            foreach (var kvp in _metrics)
            {
                var values = kvp.Value;
                if (values.Count > 0)
                {
                    stats.Operations[kvp.Key] = new OperationStats
                    {
                        Count = values.Count,
                        AverageDuration = values.Average(),
                        MinDuration = values.Min(),
                        MaxDuration = values.Max(),
                        TotalDuration = values.Sum()
                    };
                }
            }

            return stats;
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (_lock)
        {
            _metrics.Clear();
            _totalOperations = 0;
        }
    }

    private class PerformanceTrace : IDisposable
    {
        private readonly PerformanceMonitor _monitor;
        private readonly string _operationName;
        private readonly DateTime _startTime;
        private bool _disposed;

        public PerformanceTrace(PerformanceMonitor monitor, string operationName)
        {
            _monitor = monitor;
            _operationName = operationName;
            _startTime = DateTime.UtcNow;
            _disposed = false;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                var elapsed = (DateTime.UtcNow - _startTime).TotalMilliseconds;
                _monitor.RecordMetric(_operationName, elapsed);
                _disposed = true;
            }
        }
    }
}

