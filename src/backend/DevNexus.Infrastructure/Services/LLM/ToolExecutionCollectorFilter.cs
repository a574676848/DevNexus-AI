using DevNexus.Core.Models.Evaluation;
using DevNexus.Core.Abstractions;
using DevNexus.Core.Services.Chat;
using DevNexus.Core.Services.Tools;
using DevNexus.Domain.Abstractions;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Constants;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;

namespace DevNexus.Infrastructure.Services.LLM;

/// <summary>
/// 工具执行收集过滤器 (Phase 4)
/// 用于监控宿主工具面及其他 SK 工具的执行结果并记录到上下文中
/// </summary>
public class ToolExecutionCollectorFilter : IAutoFunctionInvocationFilter
{
    private const int MaxErrorSummaryLength = 200;

    private readonly ILogger<ToolExecutionCollectorFilter> _logger;
    private readonly ITokenAuditQueue _auditQueue;
    private readonly IToolInvocationValidationService _validationService;
    private readonly IToolCatalogService _toolCatalogService;
    private readonly IServiceProvider _serviceProvider;

    public ToolExecutionCollectorFilter(
        ILogger<ToolExecutionCollectorFilter> logger,
        ITokenAuditQueue auditQueue,
        IToolInvocationValidationService validationService,
        IToolCatalogService toolCatalogService,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _auditQueue = auditQueue;
        _validationService = validationService;
        _toolCatalogService = toolCatalogService;
        _serviceProvider = serviceProvider;
    }

    public async Task OnAutoFunctionInvocationAsync(
        AutoFunctionInvocationContext context,
        Func<AutoFunctionInvocationContext, Task> next)
    {
        var functionName = context.Function.Name;
        var pluginName = context.Function.PluginName;
        var sw = Stopwatch.StartNew();

        // 某些参数可能很长，这里记录简要日志
        _logger.LogDebug("[AgentLoop.Collector] Invoking {Plugin}.{Function}", pluginName, functionName);

        var toolName = $"{pluginName}.{functionName}";
        var argumentsJson = JsonSerializer.Serialize(context.Arguments);
        var validation = _validationService.Validate(toolName, argumentsJson);
        if (!validation.IsValid)
        {
            sw.Stop();
            var validationRecord = new ToolExecutionRecord
            {
                ToolCallId = ChatExecutionContext.CurrentToolCallId,
                ToolName = toolName,
                Arguments = argumentsJson,
                Success = false,
                FailureReason = ToolFailureReason.ToolFormatError,
                Retryable = validation.Retryable,
                RequiresHumanIntervention = false,
                SuggestedAction = ToolSuggestedAction.PromptUserInput,
                UserMessage = validation.UserMessage,
                Output = validation.UserMessage ?? "工具参数验证失败。",
                ErrorMessage = validation.UserMessage,
                ErrorSummary = validation.UserMessage,
                Duration = sw.Elapsed
            };

            if (ChatExecutionContext.HasActive)
            {
                ChatExecutionContext.AddToolRecord(validationRecord);
                await QueueToolAuditRecordAsync(validationRecord, pluginName, functionName, sw.ElapsedMilliseconds, argumentsValid: false);
                await NotifyToolInvocationAsync(
                    ChatExecutionContext.CurrentToolCallId,
                    pluginName,
                    functionName,
                    ToolInvocationStatus.Failed,
                    sw.ElapsedMilliseconds,
                    validationRecord.ErrorSummary);
            }

            _logger.LogWarning(
                "[AgentLoop.Collector] 工具参数预验证失败 | Tool={Tool} Reason={Reason}",
                toolName,
                validation.FailureReason);
            return;
        }

        await next(context);

        sw.Stop();

        // 仅在会话上下文中有效时记录
        if (!ChatExecutionContext.HasActive)
        {
            return;
        }

        var resultObj = context.Result.GetValue<object>();
        var outputString = resultObj?.ToString() ?? string.Empty;

        var classification = ToolExecutionResultClassifier.Classify(
            outputString,
            RequiresTaggedOutput(toolName));

        string? errorMessage = null;
        string? errorSummary = null;
        int exitCode = 0;

        if (!classification.Success)
        {
            errorMessage = outputString;
            errorSummary = ToolOutputBudgetCompressor.Compress(outputString, MaxErrorSummaryLength);
            exitCode = ExtractExitCode(outputString);
        }

        // 构造记录
        var record = new ToolExecutionRecord
        {
            ToolCallId = ChatExecutionContext.CurrentToolCallId,
            ToolName = toolName,
            Arguments = argumentsJson,
            Success = classification.Success,
            FailureReason = classification.FailureReason,
            Retryable = classification.Retryable,
            RequiresHumanIntervention = classification.RequiresHumanIntervention,
            ShouldFallback = classification.ShouldFallback,
            ShouldRotateCredential = classification.ShouldRotateCredential,
            SuggestedAction = classification.SuggestedAction,
            UserMessage = classification.UserMessage,
            RequestedUserInputKind = classification.RequestedUserInputKind,
            RequestedUserInputLabel = classification.RequestedUserInputLabel,
            Output = ToolOutputBudgetCompressor.Compress(outputString, AiOptimizationConstants.ToolOutputContextMaxChars),
            ErrorMessage = errorMessage,
            ErrorSummary = errorSummary,
            ExitCode = exitCode,
            Duration = sw.Elapsed
        };

        ChatExecutionContext.AddToolRecord(record);
        await QueueToolAuditRecordAsync(record, pluginName, functionName, sw.ElapsedMilliseconds);
        await NotifyToolInvocationAsync(
            ChatExecutionContext.CurrentToolCallId,
            pluginName,
            functionName,
            classification.Success ? ToolInvocationStatus.Completed : ToolInvocationStatus.Failed,
            sw.ElapsedMilliseconds,
            errorSummary);

        _logger.LogInformation(
            "[AgentLoop.Collector] 收集工具执行记录: {Tool} | 成功={Success} | 耗时={Duration}ms",
            record.ToolName, classification.Success, sw.ElapsedMilliseconds);
    }

    private async Task NotifyToolInvocationAsync(
        Guid toolCallId,
        string? pluginName,
        string? functionName,
        ToolInvocationStatus status,
        long durationMs,
        string? errorMessage)
    {
        try
        {
            var ctx = TokenAuditContext.Current;
            if (toolCallId == Guid.Empty || ctx?.OwnerUserId is not Guid userId || userId == Guid.Empty)
            {
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var notifier = scope.ServiceProvider.GetService<IToolInvocationNotifier>();
            if (notifier == null)
            {
                return;
            }

            await notifier.NotifyToolInvocationAsync(userId, new ToolInvocationDto
            {
                SessionId = ctx.SessionId ?? Guid.Empty,
                MessageId = ctx.MessageId ?? Guid.Empty,
                ToolCallId = toolCallId,
                PluginName = pluginName ?? "unknown",
                FunctionName = functionName ?? "unknown",
                Status = status.ToWireValue(),
                DurationMs = durationMs,
                ErrorMessage = errorMessage
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[AgentLoop.Collector] 工具状态通知失败 | ToolCallId={ToolCallId}",
                toolCallId);
        }
    }

    private async Task QueueToolAuditRecordAsync(
        ToolExecutionRecord toolRecord,
        string? pluginName,
        string? functionName,
        long durationMs,
        bool argumentsValid = true)
    {
        try
        {
            var ctx = TokenAuditContext.Current;
            var toolName = toolRecord.ToolName;
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
                ModelId = ctx?.ModelName ?? "tool",
                ProviderType = ctx?.ProviderType ?? ModelInvocationProviderTypes.Llm,
                ProviderName = ctx?.ProviderName ?? "tool",
                ProviderId = ctx?.LLMProviderId.ToString() ?? Guid.Empty.ToString(),
                MeteringType = ModelInvocationMeteringTypes.Request,
                MeteringValue = 1,
                UsageSource = ModelInvocationUsageSources.None,
                Status = toolRecord.Success ? ModelInvocationStatuses.Succeeded : ModelInvocationStatuses.Failed,
                ErrorCode = toolRecord.Success ? null : toolRecord.FailureReason.ToWireValue(),
                ErrorMessage = toolRecord.ErrorSummary,
                DurationMs = durationMs,
                ToolName = string.IsNullOrWhiteSpace(toolName)
                    ? $"{pluginName}.{functionName}"
                    : toolName,
                ToolArgumentsValid = argumentsValid && toolRecord.FailureReason != ToolFailureReason.ToolFormatError,
                ToolFailureReason = toolRecord.FailureReason.ToWireValue(),
                ToolSuggestedAction = toolRecord.SuggestedAction.ToWireValue(),
                ToolRetryable = toolRecord.Retryable,
                ToolRequiresHumanIntervention = toolRecord.RequiresHumanIntervention,
                ToolExitCode = toolRecord.ExitCode
            };

            await _auditQueue.QueueBackgroundWorkItemAsync(record);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[AgentLoop.Collector] 工具调用审计入队失败 | Tool={Tool}",
                toolRecord.ToolName);
        }
    }

    private static int ExtractExitCode(string input)
    {
        // ✅ 改进：支持中英文退出码提取
        if (string.IsNullOrEmpty(input)) return 0;

        // 匹配中文：退出码: 1
        if (input.Contains("退出码:"))
        {
            var parts = input.Split("退出码:");
            if (parts.Length > 1)
            {
                var codeStr = parts[1].Split('\n')[0].Trim();
                if (int.TryParse(codeStr, out int code)) return code;
            }
        }

        // 匹配英文：Exit Code: 1
        if (input.Contains("Exit Code:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = input.Split(new[] { "Exit Code:" }, StringSplitOptions.None);
            if (parts.Length > 1)
            {
                var codeStr = parts[1].Split('\n')[0].Trim();
                if (int.TryParse(codeStr, out int code)) return code;
            }
        }

        return 0;
    }

    private bool RequiresTaggedOutput(string? toolName)
    {
        var pluginName = _toolCatalogService.ResolvePluginName(ToolInvocationNameParser.Parse(toolName).PluginName);
        return _toolCatalogService
            .GetAllTools()
            .Any(tool => string.Equals(tool.PluginName, pluginName, StringComparison.Ordinal)
                         && tool.RequiresTaggedOutput);
    }
}
