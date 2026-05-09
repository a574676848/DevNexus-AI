using System.Diagnostics;

namespace DevNexus.Core.Services.Observability;

/// <summary>
/// 分布式追踪上下文 - 扩展 AsyncLocal 以支持 W3C TraceContext 标准
/// 用于在异步调用链中传递分布式追踪信息（TraceId, SpanId, ParentSpanId）
/// </summary>
public static class TraceContext
{
    private static readonly AsyncLocal<string> _traceId = new();
    private static readonly AsyncLocal<string> _spanId = new();
    private static readonly AsyncLocal<string> _parentSpanId = new();
    private static readonly AsyncLocal<string> _traceState = new();

    /// <summary>
    /// 当前 TraceId (W3C 标准格式：32 字符十六进制)
    /// </summary>
    public static string CurrentTraceId
    {
        get => _traceId.Value ?? Activity.Current?.Id?.Split('-')[0] ?? GenerateTraceId();
        set => _traceId.Value = value;
    }

    /// <summary>
    /// 当前 SpanId (W3C 标准格式：16 字符十六进制)
    /// </summary>
    public static string CurrentSpanId
    {
        get => _spanId.Value ?? Activity.Current?.Id?.Split('-')[1] ?? GenerateSpanId();
        set => _spanId.Value = value;
    }

    /// <summary>
    /// 父 SpanId
    /// </summary>
    public static string CurrentParentSpanId
    {
        get => _parentSpanId.Value ?? Activity.Current?.ParentId?.Split('-')[1] ?? "";
        set => _parentSpanId.Value = value;
    }

    /// <summary>
    /// TraceState (OTEL 可选字段)
    /// </summary>
    public static string CurrentTraceState
    {
        get => _traceState.Value ?? "";
        set => _traceState.Value = value;
    }

    /// <summary>
    /// 创建一个新的追踪上下文作用域
    /// 通常在请求入口调用一次，后续异步操作会继承该上下文
    /// </summary>
    /// <param name="traceId">追踪 ID，如果为空则自动生成</param>
    /// <param name="spanId">Span ID，如果为空则自动生成</param>
    /// <returns>返回当前的追踪信息（便于日志记录）</returns>
    public static TraceContextSnapshot BeginTrace(string? traceId = null, string? spanId = null)
    {
        var newTraceId = traceId ?? GenerateTraceId();
        var newSpanId = spanId ?? GenerateSpanId();

        _traceId.Value = newTraceId;
        _spanId.Value = newSpanId;
        _parentSpanId.Value = "";
        _traceState.Value = "";

        return new TraceContextSnapshot(newTraceId, newSpanId, "", "");
    }

    /// <summary>
    /// 创建一个子 Span（用于跨越异步边界）
    /// </summary>
    /// <param name="operationName">操作名称，用于日志记录</param>
    /// <returns>返回新的 Span 信息，异步操作完成后应调用 EndSpan</returns>
    public static TraceContextSnapshot BeginChildSpan(string operationName)
    {
        var parentSpanId = CurrentSpanId;
        var newSpanId = GenerateSpanId();

        _parentSpanId.Value = parentSpanId;
        _spanId.Value = newSpanId;

        return new TraceContextSnapshot(CurrentTraceId, newSpanId, parentSpanId, "");
    }

    /// <summary>
    /// 结束当前 Span，恢复到父 SpanId
    /// </summary>
    public static void EndSpan()
    {
        if (!string.IsNullOrEmpty(CurrentParentSpanId))
        {
            _spanId.Value = CurrentParentSpanId;
            _parentSpanId.Value = "";
        }
    }

    /// <summary>
    /// 获取当前追踪快照（用于日志记录）
    /// </summary>
    public static TraceContextSnapshot GetCurrentSnapshot() =>
        new(CurrentTraceId, CurrentSpanId, CurrentParentSpanId, CurrentTraceState);

    /// <summary>
    /// 清理追踪上下文
    /// </summary>
    public static void Clear()
    {
        _traceId.Value = "";
        _spanId.Value = "";
        _parentSpanId.Value = "";
        _traceState.Value = "";
    }

    /// <summary>
    /// 生成一个 W3C 兼容的 TraceId (32 字符十六进制)
    /// </summary>
    private static string GenerateTraceId()
    {
        var bytes = new byte[16];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return BitConverter.ToString(bytes).Replace("-", "").ToLower();
    }

    /// <summary>
    /// 生成一个 W3C 兼容的 SpanId (16 字符十六进制)
    /// </summary>
    private static string GenerateSpanId()
    {
        var bytes = new byte[8];
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return BitConverter.ToString(bytes).Replace("-", "").ToLower();
    }
}

/// <summary>
/// 追踪上下文的快照（不可变的值对象）
/// </summary>
public record TraceContextSnapshot(
    string TraceId,
    string SpanId,
    string ParentSpanId,
    string TraceState)
{
    /// <summary>
    /// 返回 W3C traceparent 标准格式: version-traceId-spanId-traceFlags
    /// </summary>
    public string ToTraceParentHeader() => $"00-{TraceId}-{SpanId}-01";

    /// <summary>
    /// 返回 W3C tracestate 标准格式
    /// </summary>
    public string ToTraceStateHeader() => TraceState;

    /// <summary>
    /// 用于日志记录的格式化字符串
    /// </summary>
    public override string ToString() => 
        $"TraceId={TraceId}, SpanId={SpanId}, ParentSpanId={ParentSpanId}";
}
