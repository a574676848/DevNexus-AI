using DevNexus.Core.Abstractions.Observability;
using DevNexus.Core.Models.Observability;
using DevNexus.Core.Services.Chat;
using DevNexus.Core.Services.Observability;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace DevNexus.Infrastructure.Services.Observability;

/// <summary>
/// Seq 追踪服务实现 - 负责将结构化日志和指标发送到 Seq
/// </summary>
public class SeqTracingService : IDistributedTracingService
{
    private readonly ILogger<SeqTracingService> _logger;
    private readonly Serilog.ILogger _serilogLogger;

    public SeqTracingService(ILogger<SeqTracingService> logger)
    {
        _logger = logger;
        // 获取 Serilog 的全局日志对象（用于结构化日志输出）
        _serilogLogger = Serilog.Log.ForContext<SeqTracingService>();
    }

    /// <summary>
    /// 发送结构化日志到 Seq
    /// </summary>
    public async Task LogStructuredEventAsync(
        TraceEvent traceEvent,
        string level = "Information",
        string? message = null,
        Exception? exception = null)
    {
        try
        {
            // 构建结构化日志入口
            var logEntry = BuildLogEntry(traceEvent, level, message, exception);

            // 获取日志级别
            var logLevel = ParseLogLevel(level);

            // 转换为 Serilog 可理解的格式
            var logDict = logEntry.ToLoggingDictionary();

            // 使用 Serilog 写入结构化日志（自动发送到 Seq）
            // Serilog 会自动解析日志消息中的结构化属性
            var loggerWithContext = _serilogLogger
                .ForContext("TraceId", logEntry.TraceId)
                .ForContext("SpanId", logEntry.SpanId)
                .ForContext("SessionId", logEntry.SessionId)
                .ForContext("MessageId", logEntry.MessageId)
                .ForContext("TenantId", logEntry.TenantId)
                .ForContext("UserId", logEntry.UserId)
                .ForContext("Topic", logEntry.Topic)
                .ForContext("EventType", logEntry.EventType);

            switch (logLevel)
            {
                case Microsoft.Extensions.Logging.LogLevel.Trace:
                    loggerWithContext.Verbose("[{EventType}] {Message}", traceEvent, message);
                    break;
                case Microsoft.Extensions.Logging.LogLevel.Debug:
                    loggerWithContext.Debug("[{EventType}] {Message}", traceEvent, message);
                    break;
                case Microsoft.Extensions.Logging.LogLevel.Information:
                    loggerWithContext.Information("[{EventType}] {Message}", traceEvent, message);
                    break;
                case Microsoft.Extensions.Logging.LogLevel.Warning:
                    loggerWithContext.Warning("[{EventType}] {Message}", traceEvent, message);
                    break;
                case Microsoft.Extensions.Logging.LogLevel.Error:
                    loggerWithContext.Error(exception, "[{EventType}] {Message}", traceEvent, message);
                    break;
                case Microsoft.Extensions.Logging.LogLevel.Critical:
                    loggerWithContext.Fatal(exception, "[{EventType}] {Message}", traceEvent, message);
                    break;
                default:
                    loggerWithContext.Information("[{EventType}] {Message}", traceEvent, message);
                    break;
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            // 如果日志记录失败，记录到本地日志，避免异常继续传播
            _logger.LogError(ex, "Failed to log structured event: {EventType}", traceEvent);
        }
    }

    /// <summary>
    /// 记录性能指标到 Seq
    /// </summary>
    public async Task RecordMetricAsync(
        string metricName,
        double value,
        string unit = "",
        Dictionary<string, string>? tags = null)
    {
        try
        {
            var timestamp = DateTime.UtcNow;
            var traceSnapshot = TraceContext.GetCurrentSnapshot();

            // 构建指标日志上下文
            var loggerWithContext = _serilogLogger
                .ForContext("MetricName", metricName)
                .ForContext("Value", value)
                .ForContext("Unit", unit ?? "")
                .ForContext("Timestamp", timestamp)
                .ForContext("TraceId", traceSnapshot.TraceId)
                .ForContext("SpanId", traceSnapshot.SpanId)
                .ForContext("SessionId", ChatExecutionContext.CurrentSessionId)
                .ForContext("MessageId", ChatExecutionContext.CurrentMessageId);

            // 添加标签
            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    loggerWithContext = loggerWithContext.ForContext($"Tag_{tag.Key}", tag.Value);
                }
            }

            // 发送到 Seq
            loggerWithContext.Information("📊 Metric: {MetricName} = {Value} {Unit}",
                metricName, value, unit);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record metric: {MetricName}", metricName);
        }
    }

    /// <summary>
    /// 记录操作耗时
    /// </summary>
    public IDisposable BeginOperationTimer(string operationName)
    {
        return new OperationTimer(operationName, this, _logger);
    }

    /// <summary>
    /// 构建结构化日志入口
    /// </summary>
    private static StructuredLogEntry BuildLogEntry(
        TraceEvent traceEvent,
        string level,
        string? message,
        Exception? exception)
    {
        var traceSnapshot = TraceContext.GetCurrentSnapshot();
        
        var logEntry = new StructuredLogEntry
        {
            Timestamp = DateTime.UtcNow,
            TraceId = traceSnapshot.TraceId,
            SpanId = traceSnapshot.SpanId,
            ParentSpanId = traceSnapshot.ParentSpanId,
            Level = level,
            EventType = traceEvent.ToString(),
            Topic = DetermineTopicFromEventType(traceEvent),
            Message = message ?? traceEvent.ToString(),
            SessionId = ChatExecutionContext.CurrentSessionId,
            MessageId = ChatExecutionContext.CurrentMessageId,
            AttemptNumber = ChatExecutionContext.CurrentAttemptNumber,
            ExceptionStackTrace = exception?.StackTrace,
        };

        return logEntry;
    }

    /// <summary>
    /// 根据事件类型确定日志主题
    /// </summary>
    private static string DetermineTopicFromEventType(TraceEvent traceEvent)
    {
        return traceEvent switch
        {
            TraceEvent.AgentLoopEvaluationStarted or
            TraceEvent.AgentLoopEvaluationCompleted or
            TraceEvent.AgentLoopRepairDecided or
            TraceEvent.AgentLoopRepairAttemptStarted or
            TraceEvent.AgentLoopRepairAttemptFailed or
            TraceEvent.AgentLoopMaxAttemptsReached
                => "AgentLoop",

            TraceEvent.ToolExecutionStarted or
            TraceEvent.ToolExecutionCompleted or
            TraceEvent.ToolExecutionFailed or
            TraceEvent.ToolExecutionTimeout
                => "ToolExecution",

            TraceEvent.TerminalStreamStarted or
            TraceEvent.TerminalStreamChunkReceived or
            TraceEvent.TerminalStreamCompleted or
            TraceEvent.TerminalPersistenceStarted or
            TraceEvent.TerminalPersistenceCompleted or
            TraceEvent.TerminalPersistenceFailed or
            TraceEvent.TerminalReplayStarted
                => "Terminal",

            TraceEvent.MessageGenerationStarted or
            TraceEvent.MessageGenerationCompleted or
            TraceEvent.MessageGenerationFailed or
            TraceEvent.MessageGenerationCancelled
                => "MessageGeneration",

            TraceEvent.SessionRecoveryStarted or
            TraceEvent.SessionRecoveryCompleted or
            TraceEvent.SessionRecoveryFailed
                => "SessionRecovery",

            TraceEvent.ThinkingChainStarted or
            TraceEvent.ThinkingChainEmitted
                => "ThinkingChain",

            _ => "Other"
        };
    }

    /// <summary>
    /// 解析日志级别字符串
    /// </summary>
    private static Microsoft.Extensions.Logging.LogLevel ParseLogLevel(string level)
    {
        return level.ToLower() switch
        {
            "trace" => Microsoft.Extensions.Logging.LogLevel.Trace,
            "debug" => Microsoft.Extensions.Logging.LogLevel.Debug,
            "information" or "info" => Microsoft.Extensions.Logging.LogLevel.Information,
            "warning" or "warn" => Microsoft.Extensions.Logging.LogLevel.Warning,
            "error" => Microsoft.Extensions.Logging.LogLevel.Error,
            "critical" or "fatal" => Microsoft.Extensions.Logging.LogLevel.Critical,
            _ => Microsoft.Extensions.Logging.LogLevel.Information
        };
    }

    /// <summary>
    /// 操作计时器 - 自动记录操作耗时
    /// </summary>
    private class OperationTimer : IDisposable
    {
        private readonly string _operationName;
        private readonly SeqTracingService _tracingService;
        private readonly ILogger<SeqTracingService> _logger;
        private readonly Stopwatch _stopwatch;
        private readonly DateTime _startTime;

        public OperationTimer(
            string operationName,
            SeqTracingService tracingService,
            ILogger<SeqTracingService> logger)
        {
            _operationName = operationName;
            _tracingService = tracingService;
            _logger = logger;
            _stopwatch = Stopwatch.StartNew();
            _startTime = DateTime.UtcNow;
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            var durationMs = _stopwatch.ElapsedMilliseconds;

            // 记录性能数据
            var metricTags = new Dictionary<string, string>
            {
                { "OperationName", _operationName },
                { "Status", "Completed" }
            };

            _tracingService.RecordMetricAsync(
                $"operation_duration_ms",
                durationMs,
                "ms",
                metricTags).GetAwaiter().GetResult();

            // 显式记录到本地日志（作为备份）
            _logger.LogInformation(
                "⏱️ Operation completed: {OperationName} took {DurationMs}ms",
                _operationName,
                durationMs);
        }
    }
}
