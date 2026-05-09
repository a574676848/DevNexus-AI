using Microsoft.JSInterop;
namespace DevNexus.Client.Web.Services;

/// <summary>
/// Web 性能监控服务实现
/// </summary>
public class WebPerformanceMonitor : IPerformanceMonitor
{
    private readonly Dictionary<string, (DateTime Start, double Total, int Count)> _traces = new();
    private readonly Dictionary<string, OperationStats> _stats = new();
    private DateTime _startTime = DateTime.UtcNow;

    public IDisposable StartTrace(string operationName)
    {
        if (!_traces.ContainsKey(operationName))
        {
            _traces[operationName] = (DateTime.UtcNow, 0, 0);
        }
        return new TraceDisposable(this, operationName);
    }

    public void RecordMetric(string name, double value, string unit = "ms")
    {
        lock (_stats)
        {
            if (!_stats.ContainsKey(name))
            {
                _stats[name] = new OperationStats();
            }

            var stat = _stats[name];
            stat.Count++;
            stat.TotalDuration += value;
            stat.AverageDuration = stat.TotalDuration / stat.Count;
            stat.MinDuration = stat.MinDuration == 0 ? value : Math.Min(stat.MinDuration, value);
            stat.MaxDuration = Math.Max(stat.MaxDuration, value);
        }
    }

    public PerformanceStats GetStats()
    {
        lock (_stats)
        {
            return new PerformanceStats
            {
                TotalOperations = _stats.Values.Sum(s => s.Count),
                Operations = new Dictionary<string, OperationStats>(_stats),
                MemoryUsageMB = GC.GetTotalMemory(false) / 1024 / 1024,
                Uptime = DateTime.UtcNow - _startTime
            };
        }
    }

    public void Reset()
    {
        _traces.Clear();
        _stats.Clear();
        _startTime = DateTime.UtcNow;
    }

    private void EndTrace(string operationName)
    {
        if (_traces.TryGetValue(operationName, out var trace))
        {
            var elapsed = (DateTime.UtcNow - trace.Start).TotalMilliseconds;
            RecordMetric(operationName, elapsed);
        }
    }

    private class TraceDisposable : IDisposable
    {
        private readonly WebPerformanceMonitor _monitor;
        private readonly string _operationName;
        private bool _disposed;

        public TraceDisposable(WebPerformanceMonitor monitor, string operationName)
        {
            _monitor = monitor;
            _operationName = operationName;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _monitor.EndTrace(_operationName);
                _disposed = true;
            }
        }
    }
}

