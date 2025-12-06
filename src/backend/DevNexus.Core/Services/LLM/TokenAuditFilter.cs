using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Diagnostics;

namespace DevNexus.Core.Services.LLM;

/// <summary>
/// Token 审计过滤器
/// 记录每次 LLM 调用的 Token 消耗到 Seq
/// </summary>
public class TokenAuditFilter : IAutoFunctionInvocationFilter
{
    private readonly ILogger<TokenAuditFilter> _logger;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public TokenAuditFilter(ILogger<TokenAuditFilter> logger)
    {
        _logger = logger;
    }
    
    /// <inheritdoc />
    public async Task OnAutoFunctionInvocationAsync(
        AutoFunctionInvocationContext context, 
        Func<AutoFunctionInvocationContext, Task> next)
    {
        var functionName = context.Function.Name;
        var pluginName = context.Function.PluginName;
        var stopwatch = Stopwatch.StartNew();
        
        _logger.LogInformation(
            "[AI.Function.Invoking] Function invocation started | Plugin={Plugin} Function={Function}",
            pluginName,
            functionName);
        
        try
        {
            await next(context);
            
            stopwatch.Stop();
            
            _logger.LogInformation(
                "[AI.Function.Invoked] Function invocation completed | Plugin={Plugin} Function={Function} Duration={Duration}ms",
                pluginName,
                functionName,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(
                ex,
                "[AI.Function.Error] Function invocation failed | Plugin={Plugin} Function={Function} Duration={Duration}ms Error={Error}",
                pluginName,
                functionName,
                stopwatch.ElapsedMilliseconds,
                ex.Message);
            
            throw;
        }
    }
}

/// <summary>
/// Token 使用量审计记录
/// </summary>
public class TokenUsageRecord
{
    /// <summary>
    /// 记录ID
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// 会话ID
    /// </summary>
    public Guid SessionId { get; set; }
    
    /// <summary>
    /// 消息ID
    /// </summary>
    public Guid MessageId { get; set; }
    
    /// <summary>
    /// 用户ID
    /// </summary>
    public Guid UserId { get; set; }
    
    /// <summary>
    /// 模型ID
    /// </summary>
    public string ModelId { get; set; } = string.Empty;
    
    /// <summary>
    /// 输入 Token 数
    /// </summary>
    public int InputTokens { get; set; }
    
    /// <summary>
    /// 输出 Token 数
    /// </summary>
    public int OutputTokens { get; set; }
    
    /// <summary>
    /// 总 Token 数
    /// </summary>
    public int TotalTokens => InputTokens + OutputTokens;
    
    /// <summary>
    /// 处理时间（毫秒）
    /// </summary>
    public long DurationMs { get; set; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Token 审计服务
/// 用于记录和查询 Token 使用量
/// </summary>
public class TokenAuditService
{
    private readonly ILogger<TokenAuditService> _logger;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public TokenAuditService(ILogger<TokenAuditService> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// 记录 Token 使用量
    /// </summary>
    /// <param name="record">使用量记录</param>
    public void RecordUsage(TokenUsageRecord record)
    {
        // 使用结构化日志记录到 Seq
        _logger.LogInformation(
            "[AI.TokenAudit] Token usage recorded | " +
            "SessionId={SessionId} MessageId={MessageId} UserId={UserId} " +
            "Model={Model} InputTokens={InputTokens} OutputTokens={OutputTokens} " +
            "TotalTokens={TotalTokens} Duration={Duration}ms",
            record.SessionId,
            record.MessageId,
            record.UserId,
            record.ModelId,
            record.InputTokens,
            record.OutputTokens,
            record.TotalTokens,
            record.DurationMs);
    }
    
    /// <summary>
    /// 记录流式完成的 Token 使用量
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="messageId">消息ID</param>
    /// <param name="userId">用户ID</param>
    /// <param name="modelId">模型ID</param>
    /// <param name="inputTokens">输入 Token 数</param>
    /// <param name="outputTokens">输出 Token 数</param>
    /// <param name="durationMs">处理时间</param>
    public void RecordStreamingCompletion(
        Guid sessionId,
        Guid messageId,
        Guid userId,
        string modelId,
        int inputTokens,
        int outputTokens,
        long durationMs)
    {
        var record = new TokenUsageRecord
        {
            SessionId = sessionId,
            MessageId = messageId,
            UserId = userId,
            ModelId = modelId,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            DurationMs = durationMs
        };
        
        RecordUsage(record);
    }
}
