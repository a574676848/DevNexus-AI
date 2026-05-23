using DevNexus.Domain.Abstractions;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Text.Json;
using System.Diagnostics;
using DevNexus.Core.Services.Chat;

namespace DevNexus.Infrastructure.Services.LLM;

/// <summary>
/// Token 审计过滤器
/// 记录每次 LLM 函数调用的 Token 消耗到 Seq 和数据库
/// </summary>
public class TokenAuditFilter : IAutoFunctionInvocationFilter
{
    private readonly ILogger<TokenAuditFilter> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITokenAuditQueue _auditQueue;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="serviceProvider">服务提供者</param>
    /// <param name="auditQueue">Token 审计队列</param>
    public TokenAuditFilter(
        ILogger<TokenAuditFilter> logger,
        IServiceProvider serviceProvider,
        ITokenAuditQueue auditQueue)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _auditQueue = auditQueue;
    }

    /// <inheritdoc />
    public async Task OnAutoFunctionInvocationAsync(
        AutoFunctionInvocationContext context,
        Func<AutoFunctionInvocationContext, Task> next)
    {
        var functionName = context.Function.Name;
        var pluginName = context.Function.PluginName;
        var stopwatch = Stopwatch.StartNew();
        var toolCallId = Guid.NewGuid();

        _logger.LogInformation(
            "[AI.Function.Invoking] Function invocation started | Plugin={Plugin} Function={Function}",
            pluginName,
            functionName);

        ChatExecutionContext.PushToolCallId(toolCallId);
        await NotifyToolInvocationAsync(toolCallId, pluginName, functionName, ToolInvocationStatus.Running, null, null);

        try
        {
            await next(context);

            stopwatch.Stop();

            await NotifyToolInvocationAsync(
                toolCallId,
                pluginName,
                functionName,
                ToolInvocationStatus.Completed,
                stopwatch.ElapsedMilliseconds,
                null);

            // 尝试从 Result 的 Metadata 中提取 Token 使用量
            var (inputTokens, outputTokens) = ExtractTokenUsageFromResult(context.Result);

            if (inputTokens.HasValue && outputTokens.HasValue)
            {
                _logger.LogInformation(
                    "[AI.Function.TokenUsage] Function token usage | Plugin={Plugin} Function={Function} " +
                    "InputTokens={InputTokens} OutputTokens={OutputTokens} TotalTokens={TotalTokens} Duration={Duration}ms",
                    pluginName,
                    functionName,
                    inputTokens.Value,
                    outputTokens.Value,
                    inputTokens.Value + outputTokens.Value,
                    stopwatch.ElapsedMilliseconds);

                // 异步持久化到数据库（不阻塞主流程）
                _ = PersistFunctionTokenUsageAsync(
                    pluginName,
                    functionName,
                    inputTokens.Value,
                    outputTokens.Value,
                    stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogInformation(
                    "[AI.Function.Invoked] Function invocation completed | Plugin={Plugin} Function={Function} Duration={Duration}ms",
                    pluginName,
                    functionName,
                    stopwatch.ElapsedMilliseconds);
            }
        }
        catch (OperationCanceledException ex)
        {
            stopwatch.Stop();

            await NotifyToolInvocationAsync(
                toolCallId,
                pluginName,
                functionName,
                ToolInvocationStatus.Cancelled,
                stopwatch.ElapsedMilliseconds,
                ex.Message);

            _logger.LogWarning(
                ex,
                "[AI.Function.Cancelled] Function invocation cancelled | Plugin={Plugin} Function={Function} Duration={Duration}ms Error={Error}",
                pluginName,
                functionName,
                stopwatch.ElapsedMilliseconds,
                ex.Message);

            throw;
        }
        catch (TimeoutException ex)
        {
            stopwatch.Stop();

            await NotifyToolInvocationAsync(
                toolCallId,
                pluginName,
                functionName,
                ToolInvocationStatus.Timeout,
                stopwatch.ElapsedMilliseconds,
                ex.Message);

            _logger.LogError(
                ex,
                "[AI.Function.Timeout] Function invocation timeout | Plugin={Plugin} Function={Function} Duration={Duration}ms Error={Error}",
                pluginName,
                functionName,
                stopwatch.ElapsedMilliseconds,
                ex.Message);

            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            await NotifyToolInvocationAsync(
                toolCallId,
                pluginName,
                functionName,
                ToolInvocationStatus.Failed,
                stopwatch.ElapsedMilliseconds,
                ex.Message);

            _logger.LogError(
                ex,
                "[AI.Function.Error] Function invocation failed | Plugin={Plugin} Function={Function} Duration={Duration}ms Error={Error}",
                pluginName,
                functionName,
                stopwatch.ElapsedMilliseconds,
                ex.Message);

            throw;
        }
        finally
        {
            ChatExecutionContext.PopToolCallId();
        }
    }

    private async Task NotifyToolInvocationAsync(
        Guid toolCallId,
        string? pluginName,
        string? functionName,
        ToolInvocationStatus status,
        long? durationMs,
        string? errorMessage)
    {
        try
        {
            var ctx = TokenAuditContext.Current;
            if (ctx == null || ctx.OwnerUserId == null || ctx.OwnerUserId == Guid.Empty)
            {
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var notifier = scope.ServiceProvider.GetService<IToolInvocationNotifier>();
            if (notifier == null)
            {
                return;
            }

            var invocation = new ToolInvocationDto
            {
                SessionId = ctx.SessionId ?? Guid.Empty,
                MessageId = ctx.MessageId ?? Guid.Empty,
                ToolCallId = toolCallId,
                PluginName = pluginName ?? "unknown",
                FunctionName = functionName ?? "unknown",
                Status = status.ToWireValue(),
                DurationMs = durationMs,
                ErrorMessage = errorMessage
            };

            await notifier.NotifyToolInvocationAsync(ctx.OwnerUserId.Value, invocation);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[AI.Function.Notify] Failed to notify tool invocation | ToolCallId={ToolCallId}",
                toolCallId);
        }
    }

    /// <summary>
    /// 异步持久化函数调用 Token 使用量到数据库
    /// </summary>
    private async Task PersistFunctionTokenUsageAsync(
        string? pluginName,
        string? functionName,
        int inputTokens,
        int outputTokens,
        long durationMs)
    {
        try
        {
            // 从 AsyncLocal 上下文获取会话、主体和场景信息
            var ctx = TokenAuditContext.Current;

            var record = new ModelInvocationAuditRecord
            {
                OwnerType = ctx?.OwnerType ?? ModelInvocationOwnerTypes.System,
                OwnerUserId = ctx?.OwnerUserId,
                InvocationKind = ModelInvocationKinds.FunctionCall,
                SceneCode = ModelInvocationSceneCodes.ToolFunctionCall,
                SceneCategory = ctx?.SceneCategory ?? ModelInvocationSceneCategories.Other,
                ResourceType = ctx?.ResourceType ?? ModelInvocationResourceTypes.None,
                ResourceId = ctx?.ResourceId,
                SessionId = ctx?.SessionId,
                MessageId = ctx?.MessageId,
                TraceId = ctx?.TraceId,
                ParentInvocationId = ctx?.ParentInvocationId,
                RootInvocationId = ctx?.RootInvocationId,
                ModelId = ctx?.ModelName ?? "unknown",
                ProviderName = ctx?.ProviderName ?? "unknown",
                ProviderId = ctx?.LLMProviderId.ToString() ?? Guid.Empty.ToString(),
                MeteringType = ModelInvocationMeteringTypes.Token,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                TotalTokens = inputTokens + outputTokens,
                MeteringValue = inputTokens + outputTokens,
                UsageSource = ModelInvocationUsageSources.Actual,
                Status = ModelInvocationStatuses.Succeeded,
                DurationMs = durationMs
            };

            LogPromptDiagnostics(ctx, inputTokens, ctx?.CachedPromptTokens, "function");
            await _auditQueue.QueueBackgroundWorkItemAsync(record);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AI.Function.TokenUsage] Failed to queue function token usage");
        }
    }

    /// <summary>
    /// 从 FunctionResult 的 Metadata 中提取 Token 使用量
    /// </summary>
    /// <param name="result">函数执行结果</param>
    /// <returns>输入和输出 Token 数量元组</returns>
    private static (int? InputTokens, int? OutputTokens) ExtractTokenUsageFromResult(FunctionResult? result)
    {
        if (result?.Metadata == null)
        {
            return (null, null);
        }

        // 尝试从 "Usage" 键获取 Token 使用量
        if (result.Metadata.TryGetValue("Usage", out var usageObj) && usageObj != null)
        {
            var usageType = usageObj.GetType();

            // 尝试获取 InputTokens 和 OutputTokens 属性
            var inputTokensProp = usageType.GetProperty("InputTokens");
            var outputTokensProp = usageType.GetProperty("OutputTokens");

            if (inputTokensProp != null && outputTokensProp != null)
            {
                var inputTokens = inputTokensProp.GetValue(usageObj) as int?;
                var outputTokens = outputTokensProp.GetValue(usageObj) as int?;

                if (inputTokens.HasValue && outputTokens.HasValue)
                {
                    return (inputTokens.Value, outputTokens.Value);
                }
            }

            // 兼容旧版字段名
            var promptTokensProp = usageType.GetProperty("PromptTokens");
            var completionTokensProp = usageType.GetProperty("CompletionTokens");

            if (promptTokensProp != null && completionTokensProp != null)
            {
                var promptTokens = promptTokensProp.GetValue(usageObj) as int?;
                var completionTokens = completionTokensProp.GetValue(usageObj) as int?;

                if (promptTokens.HasValue && completionTokens.HasValue)
                {
                    return (promptTokens.Value, completionTokens.Value);
                }
            }
        }

        return (null, null);
    }

    private static string? SerializeStablePrefixManifest(
        IReadOnlyList<PromptFragmentManifestItemDto>? manifest)
    {
        return manifest == null || manifest.Count == 0
            ? null
            : JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private void LogPromptDiagnostics(
        TokenAuditContext? context,
        int? inputTokens,
        int? cachedPromptTokens,
        string invocationKind)
    {
        if (context == null)
        {
            return;
        }

        var costDiagnostics = PromptCostDiagnostics.Build(new PromptCostObservation
        {
            InputTokens = inputTokens,
            CachedPromptTokens = cachedPromptTokens,
            DynamicContextTokens = context.DynamicContextTokens,
            HistoryTokens = context.HistoryTokens
        });

        _logger.LogDebug(
            "[AI.Prompt.Diagnostics] Prompt diagnostics | InvocationKind={InvocationKind} SessionId={SessionId} MessageId={MessageId} " +
            "CachedPromptTokens={CachedPromptTokens} NonCachedInputTokens={NonCachedInputTokens} CacheHitRatio={CacheHitRatio} " +
            "DynamicContextRatio={DynamicContextRatio} HistoryRatio={HistoryRatio} PromptCacheKey={PromptCacheKey} StablePrefixHash={StablePrefixHash} " +
            "ToolSchemaHash={ToolSchemaHash} DynamicContextTokens={DynamicContextTokens} HistoryTokens={HistoryTokens} " +
            "CacheMarkerCandidateCount={CacheMarkerCandidateCount} CacheDoubleMarkerReady={CacheDoubleMarkerReady} " +
            "CacheMarkerReadinessReason={CacheMarkerReadinessReason} StablePrefixManifest={StablePrefixManifest} " +
            "DynamicContextManifest={DynamicContextManifest}",
            invocationKind,
            context.SessionId,
            context.MessageId,
            cachedPromptTokens,
            costDiagnostics.NonCachedInputTokens,
            costDiagnostics.CacheHitRatio,
            costDiagnostics.DynamicContextRatio,
            costDiagnostics.HistoryRatio,
            context.PromptCacheKey,
            context.StablePrefixHash,
            context.ToolSchemaHash,
            context.DynamicContextTokens,
            context.HistoryTokens,
            context.CacheMarkerCandidateCount,
            context.CacheDoubleMarkerReady,
            context.CacheMarkerReadinessReason,
            SerializeStablePrefixManifest(context.StablePrefixManifest),
            SerializeStablePrefixManifest(context.DynamicContextManifest));
    }

}


/// <summary>
/// Token 审计服务
/// 用于记录和查询 Token 使用量
/// </summary>
public class TokenAuditService : ITokenAuditService
{
    private readonly ILogger<TokenAuditService> _logger;
    private readonly ITokenAuditQueue _auditQueue;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logger">日志记录器</param>
    /// <param name="auditQueue">Token 审计队列</param>
    public TokenAuditService(ILogger<TokenAuditService> logger, ITokenAuditQueue auditQueue)
    {
        _logger = logger;
        _auditQueue = auditQueue;
    }

    /// <summary>
    /// 记录 Token 使用量
    /// </summary>
    /// <param name="record">使用量记录</param>
    public void RecordUsage(ModelInvocationAuditRecord record)
    {
        // 使用结构化日志记录到 Seq
        _logger.LogInformation(
            "[AI.TokenAudit] Token usage recorded | " +
            "SceneCode={SceneCode} SessionId={SessionId} MessageId={MessageId} OwnerUserId={OwnerUserId} " +
            "Model={Model} InputTokens={InputTokens} OutputTokens={OutputTokens} " +
            "TotalTokens={TotalTokens} Duration={Duration}ms",
            record.SceneCode,
            record.SessionId,
            record.MessageId,
            record.OwnerUserId,
            record.ModelId,
            record.InputTokens,
            record.OutputTokens,
            record.TotalTokens ?? 0,
            record.DurationMs);
    }

    /// <summary>
    /// 记录流式完成的 Token 使用量
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <param name="messageId">消息ID</param>
    /// <param name="userId">用户ID</param>
    /// <param name="modelId">模型ID</param>
    /// <param name="providerName">提供商名称</param>
    /// <param name="providerId">LLM提供商数据库主键ID（GUID字符串）</param>
    /// <param name="inputTokens">输入 Token 数</param>
    /// <param name="outputTokens">输出 Token 数</param>
    /// <param name="durationMs">处理时间</param>
    public void RecordStreamingCompletion(
        Guid? sessionId,
        Guid? messageId,
        Guid? userId,
        string modelId,
        string providerName,
        string providerType,
        string providerId,
        int inputTokens,
        int outputTokens,
        long durationMs,
        string invocationKind = ModelInvocationKinds.ChatCompletion,
        string sceneCode = ModelInvocationSceneCodes.ChatMessageReply,
        string sceneCategory = ModelInvocationSceneCategories.UserFacing,
        string resourceType = ModelInvocationResourceTypes.Message,
        string? resourceId = null,
        string usageSource = ModelInvocationUsageSources.Actual,
        string status = ModelInvocationStatuses.Succeeded,
        string? errorCode = null,
        string? errorMessage = null,
        string meteringType = ModelInvocationMeteringTypes.Token,
        decimal? meteringValue = null,
        int? cachedPromptTokens = null,
        string? promptCacheKey = null,
        string? stablePrefixHash = null,
        string? toolSchemaHash = null,
        int? dynamicContextTokens = null,
        int? historyTokens = null,
        int? cacheMarkerCandidateCount = null,
        bool? cacheDoubleMarkerReady = null)
    {
        var auditContext = TokenAuditContext.Current;
        var resolvedUserId = userId ?? auditContext?.OwnerUserId;
        var resolvedSessionId = sessionId ?? auditContext?.SessionId;
        var resolvedMessageId = messageId ?? auditContext?.MessageId;
        var resolvedProviderId = !string.IsNullOrWhiteSpace(providerId) && providerId != Guid.Empty.ToString()
            ? providerId
            : auditContext?.LLMProviderId.ToString() ?? Guid.Empty.ToString();
        var resolvedProviderName = !string.IsNullOrWhiteSpace(providerName) && providerName != "unknown"
            ? providerName
            : auditContext?.ProviderName ?? "unknown";
        var resolvedProviderType = !string.IsNullOrWhiteSpace(providerType)
            ? providerType
            : auditContext?.ProviderType ?? ModelInvocationProviderTypes.Llm;
        var resolvedModelId = !string.IsNullOrWhiteSpace(modelId) && modelId != "unknown"
            ? modelId
            : auditContext?.ModelName ?? "unknown";

        var ownerType = resolvedUserId.HasValue && resolvedUserId.Value != Guid.Empty
            ? ModelInvocationOwnerTypes.User
            : auditContext?.OwnerType ?? ModelInvocationOwnerTypes.System;

        var record = new ModelInvocationAuditRecord
        {
            OwnerType = ownerType,
            OwnerUserId = resolvedUserId,
            InvocationKind = invocationKind,
            SceneCode = sceneCode,
            SceneCategory = sceneCategory,
            ResourceType = resourceType,
            ResourceId = resourceId,
            SessionId = resolvedSessionId,
            MessageId = resolvedMessageId,
            ModelId = resolvedModelId,
            ProviderType = resolvedProviderType,
            ProviderName = resolvedProviderName,
            ProviderId = resolvedProviderId,
            MeteringType = meteringType,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = inputTokens + outputTokens,
            MeteringValue = meteringValue ?? inputTokens + outputTokens,
            UsageSource = usageSource,
            Status = status,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            DurationMs = durationMs
        };

        LogPromptDiagnostics(auditContext, inputTokens, cachedPromptTokens ?? auditContext?.CachedPromptTokens, invocationKind);

        // 1. 记录产品化审计日志 (Seq)
        RecordUsage(record);

        // 2. 异步推入队列（不阻塞主流程）
        try
        {
            // 使用 ValueTask 的 await，Channel 写入通常是同步完成的
            _auditQueue.QueueBackgroundWorkItemAsync(record).AsTask().Wait();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[AI.TokenAudit] Failed to queue token usage | " +
                "SessionId={SessionId} MessageId={MessageId} UserId={UserId}",
                resolvedSessionId, resolvedMessageId, resolvedUserId);
        }
    }

    private void LogPromptDiagnostics(
        TokenAuditContext? context,
        int? inputTokens,
        int? cachedPromptTokens,
        string invocationKind)
    {
        if (context == null)
        {
            return;
        }

        var costDiagnostics = PromptCostDiagnostics.Build(new PromptCostObservation
        {
            InputTokens = inputTokens,
            CachedPromptTokens = cachedPromptTokens,
            DynamicContextTokens = context.DynamicContextTokens,
            HistoryTokens = context.HistoryTokens
        });

        _logger.LogDebug(
            "[AI.Prompt.Diagnostics] Prompt diagnostics | InvocationKind={InvocationKind} SessionId={SessionId} MessageId={MessageId} " +
            "CachedPromptTokens={CachedPromptTokens} NonCachedInputTokens={NonCachedInputTokens} CacheHitRatio={CacheHitRatio} " +
            "DynamicContextRatio={DynamicContextRatio} HistoryRatio={HistoryRatio} PromptCacheKey={PromptCacheKey} StablePrefixHash={StablePrefixHash} " +
            "ToolSchemaHash={ToolSchemaHash} DynamicContextTokens={DynamicContextTokens} HistoryTokens={HistoryTokens} " +
            "CacheMarkerCandidateCount={CacheMarkerCandidateCount} CacheDoubleMarkerReady={CacheDoubleMarkerReady} " +
            "CacheMarkerReadinessReason={CacheMarkerReadinessReason} StablePrefixManifest={StablePrefixManifest} " +
            "DynamicContextManifest={DynamicContextManifest}",
            invocationKind,
            context.SessionId,
            context.MessageId,
            cachedPromptTokens,
            costDiagnostics.NonCachedInputTokens,
            costDiagnostics.CacheHitRatio,
            costDiagnostics.DynamicContextRatio,
            costDiagnostics.HistoryRatio,
            context.PromptCacheKey,
            context.StablePrefixHash,
            context.ToolSchemaHash,
            context.DynamicContextTokens,
            context.HistoryTokens,
            context.CacheMarkerCandidateCount,
            context.CacheDoubleMarkerReady,
            context.CacheMarkerReadinessReason,
            SerializePromptManifest(context.StablePrefixManifest),
            SerializePromptManifest(context.DynamicContextManifest));
    }

    private static string? SerializePromptManifest(
        IReadOnlyList<PromptFragmentManifestItemDto>? manifest)
    {
        return manifest == null || manifest.Count == 0
            ? null
            : JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
}
