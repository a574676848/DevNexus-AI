using DevNexus.Core.Abstractions.Observability;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Observability;

/// <summary>
/// Agent Loop 指标收集器实现 - 收集 Agent Loop 相关的 KPI
/// </summary>
public class AgentLoopMetricsCollector : IAgentLoopMetricsCollector
{
    private readonly IDistributedTracingService _tracingService;
    private readonly ILogger<AgentLoopMetricsCollector> _logger;

    // 内存中的指标统计（可扩展为分布式存储）
    private readonly Dictionary<string, MetricAggregator> _metrics = new();
    private readonly object _lockObject = new();

    public AgentLoopMetricsCollector(
        IDistributedTracingService tracingService,
        ILogger<AgentLoopMetricsCollector> logger)
    {
        _tracingService = tracingService;
        _logger = logger;

        // 初始化关键指标
        InitializeMetrics();
    }

    /// <summary>
    /// 初始化关键指标
    /// </summary>
    private void InitializeMetrics()
    {
        _metrics["agent_loop_total_attempts"] = new MetricAggregator("agent_loop_total_attempts", "计数");
        _metrics["agent_loop_successful_repairs"] = new MetricAggregator("agent_loop_successful_repairs", "计数");
        _metrics["agent_loop_failed_repairs"] = new MetricAggregator("agent_loop_failed_repairs", "计数");
        _metrics["agent_loop_max_attempts_reached"] = new MetricAggregator("agent_loop_max_attempts_reached", "计数");
        _metrics["agent_loop_evaluation_duration"] = new MetricAggregator("agent_loop_evaluation_duration", "ms");
        _metrics["tool_execution_total"] = new MetricAggregator("tool_execution_total", "计数");
        _metrics["tool_execution_failed"] = new MetricAggregator("tool_execution_failed", "计数");
        _metrics["tool_execution_duration"] = new MetricAggregator("tool_execution_duration", "ms");
        _metrics["terminal_output_size"] = new MetricAggregator("terminal_output_size", "字节");
        _metrics["terminal_persist_latency"] = new MetricAggregator("terminal_persist_latency", "ms");
        _metrics["session_recovery_success"] = new MetricAggregator("session_recovery_success", "计数");
        _metrics["session_recovery_failed"] = new MetricAggregator("session_recovery_failed", "计数");
    }

    /// <summary>
    /// 记录自动修复尝试
    /// </summary>
    public async Task RecordRepairAttempt(bool success, long durationMs)
    {
        lock (_lockObject)
        {
            _metrics["agent_loop_total_attempts"].AddValue(1);
            _metrics["agent_loop_evaluation_duration"].AddValue(durationMs);

            if (success)
            {
                _metrics["agent_loop_successful_repairs"].AddValue(1);
            }
            else
            {
                _metrics["agent_loop_failed_repairs"].AddValue(1);
            }
        }

        // 发送到 Seq
        var tags = new Dictionary<string, string>
        {
            { "Result", success ? "Success" : "Failed" },
            { "DurationMs", durationMs.ToString() }
        };

        await _tracingService.RecordMetricAsync(
            "agent_loop_repair_attempt",
            success ? 1 : 0,
            "",
            tags);
    }

    /// <summary>
    /// 记录到达最大尝试次数
    /// </summary>
    public async Task RecordMaxAttemptsReached(int totalAttempts)
    {
        lock (_lockObject)
        {
            _metrics["agent_loop_max_attempts_reached"].AddValue(1);
        }

        var tags = new Dictionary<string, string>
        {
            { "MaxAttempts", totalAttempts.ToString() }
        };

        await _tracingService.RecordMetricAsync(
            "agent_loop_max_attempts_reached",
            totalAttempts,
            "",
            tags);
    }

    /// <summary>
    /// 记录工具执行
    /// </summary>
    public async Task RecordToolExecution(string toolName, bool success, long durationMs)
    {
        lock (_lockObject)
        {
            _metrics["tool_execution_total"].AddValue(1);
            _metrics["tool_execution_duration"].AddValue(durationMs);

            if (!success)
            {
                _metrics["tool_execution_failed"].AddValue(1);
            }
        }

        var tags = new Dictionary<string, string>
        {
            { "ToolName", toolName },
            { "Success", success ? "True" : "False" },
            { "DurationMs", durationMs.ToString() }
        };

        await _tracingService.RecordMetricAsync(
            "tool_execution",
            success ? 1 : 0,
            "",
            tags);
    }

    /// <summary>
    /// 记录终端输出
    /// </summary>
    public async Task RecordTerminalOutput(long outputBytes, int chunkCount, long persistLatencyMs)
    {
        lock (_lockObject)
        {
            _metrics["terminal_output_size"].AddValue(outputBytes);
            _metrics["terminal_persist_latency"].AddValue(persistLatencyMs);
        }

        var tags = new Dictionary<string, string>
        {
            { "OutputBytes", outputBytes.ToString() },
            { "ChunkCount", chunkCount.ToString() },
            { "PersistLatencyMs", persistLatencyMs.ToString() }
        };

        await _tracingService.RecordMetricAsync(
            "terminal_output",
            outputBytes,
            "bytes",
            tags);
    }

    /// <summary>
    /// 记录会话恢复
    /// </summary>
    public async Task RecordSessionRecovery(bool success, int recoveredMessageCount)
    {
        lock (_lockObject)
        {
            if (success)
            {
                _metrics["session_recovery_success"].AddValue(1);
            }
            else
            {
                _metrics["session_recovery_failed"].AddValue(1);
            }
        }

        var tags = new Dictionary<string, string>
        {
            { "Success", success ? "True" : "False" },
            { "RecoveredMessageCount", recoveredMessageCount.ToString() }
        };

        await _tracingService.RecordMetricAsync(
            "session_recovery",
            success ? 1 : 0,
            "",
            tags);
    }

    /// <summary>
    /// 获取所有指标的当前状态
    /// </summary>
    public Dictionary<string, object> GetMetricsSnapshot()
    {
        lock (_lockObject)
        {
            return _metrics.ToDictionary(
                kvp => kvp.Key,
                kvp => (object)kvp.Value.GetSnapshot());
        }
    }

    /// <summary>
    /// 重置所有指标
    /// </summary>
    public void ResetMetrics()
    {
        lock (_lockObject)
        {
            foreach (var aggregator in _metrics.Values)
            {
                aggregator.Reset();
            }
        }
    }
}

/// <summary>
/// 指标聚合器 - 维护单个指标的统计信息
/// </summary>
public class MetricAggregator
{
    private readonly string _name;
    private readonly string _unit;
    private readonly List<double> _values = new();

    public MetricAggregator(string name, string unit)
    {
        _name = name;
        _unit = unit;
    }

    /// <summary>
    /// 添加数值
    /// </summary>
    public void AddValue(double value)
    {
        _values.Add(value);
    }

    /// <summary>
    /// 获取快照
    /// </summary>
    public MetricSnapshot GetSnapshot()
    {
        if (_values.Count == 0)
        {
            return new MetricSnapshot(_name, _unit, 0, 0, 0, 0, 0, 0);
        }

        var sorted = _values.OrderBy(x => x).ToList();
        var sum = _values.Sum();
        var avg = sum / _values.Count;
        var min = sorted[0];
        var max = sorted[^1];
        var percentile50 = sorted[(int)(sorted.Count * 0.5)];
        var percentile95 = sorted[(int)(sorted.Count * 0.95)];
        var percentile99 = sorted[(int)(sorted.Count * 0.99)];

        return new MetricSnapshot(_name, _unit, _values.Count, sum, avg, min, max, percentile50);
    }

    /// <summary>
    /// 重置聚合器
    /// </summary>
    public void Reset()
    {
        _values.Clear();
    }
}

/// <summary>
/// 指标快照 - 某一时刻的指标统计数据
/// </summary>
public record MetricSnapshot(
    string Name,
    string Unit,
    int Count,
    double Sum,
    double Average,
    double Min,
    double Max,
    double Percentile50)
{
    /// <summary>
    /// 计算成功率
    /// </summary>
    public double CalculateSuccessRate(int successCount)
    {
        return Count == 0 ? 0 : (double)successCount / Count * 100;
    }

    /// <summary>
    /// 用于日志输出的格式化字符串
    /// </summary>
    public override string ToString()
    {
        return $"{Name} [{Unit}]: Count={Count}, Avg={Average:F2}, Min={Min:F2}, Max={Max:F2}, P50={Percentile50:F2}";
    }
}
