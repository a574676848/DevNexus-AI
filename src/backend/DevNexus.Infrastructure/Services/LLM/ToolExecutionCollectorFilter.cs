using DevNexus.Core.Models.Evaluation;
using DevNexus.Core.Services.Chat;
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
    private readonly ILogger<ToolExecutionCollectorFilter> _logger;

    public ToolExecutionCollectorFilter(ILogger<ToolExecutionCollectorFilter> logger)
    {
        _logger = logger;
    }

    public async Task OnAutoFunctionInvocationAsync(
        AutoFunctionInvocationContext context,
        Func<AutoFunctionInvocationContext, Task> next)
    {
        var functionName = context.Function.Name;
        var pluginName = context.Function.PluginName;
        var sw = Stopwatch.StartNew();

        // ✅ 工具调用开始提醒
        await ThinkingContext.EmitAsync($"🔧 正在执行: {pluginName}.{functionName}...");

        // 某些参数可能很长，这里记录简要日志
        _logger.LogDebug("[AgentLoop.Collector] Invoking {Plugin}.{Function}", pluginName, functionName);

        await next(context);

        sw.Stop();

        // 仅在会话上下文中有效时记录
        if (!ChatExecutionContext.HasActive)
        {
            return;
        }

        var resultObj = context.Result.GetValue<object>();
        var outputString = resultObj?.ToString() ?? string.Empty;

        var classification = ToolExecutionResultClassifier.Classify(outputString);

        string? errorMessage = null;
        string? errorSummary = null;
        int exitCode = 0;

        if (!classification.Success)
        {
            errorMessage = outputString;
            errorSummary = outputString.Length > 200 ? outputString[..200] + "..." : outputString;
            exitCode = ExtractExitCode(outputString);
        }

        // 构造记录
        var record = new ToolExecutionRecord
        {
            ToolName = $"{pluginName}.{functionName}",
            Arguments = JsonSerializer.Serialize(context.Arguments),
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
            Output = TruncateOutput(outputString, 2000),
            ErrorMessage = errorMessage,
            ErrorSummary = errorSummary,
            ExitCode = exitCode,
            Duration = sw.Elapsed
        };

        ChatExecutionContext.AddToolRecord(record);

        // ✅ 工具调用完成/失败提醒
        if (classification.Success)
        {
            await ThinkingContext.EmitAsync($"✅ {pluginName}.{functionName} 执行完成 (耗时 {sw.ElapsedMilliseconds}ms)");
        }
        else
        {
            await ThinkingContext.EmitAsync($"❌ {pluginName}.{functionName} 执行失败: {errorSummary}");
        }

        _logger.LogInformation(
            "[AgentLoop.Collector] 收集工具执行记录: {Tool} | 成功={Success} | 耗时={Duration}ms",
            record.ToolName, classification.Success, sw.ElapsedMilliseconds);
    }

    private static string TruncateOutput(string input, int maxLen)
    {
        if (string.IsNullOrEmpty(input) || input.Length <= maxLen) return input;
        
        // 采用 Head 500 + ... + Tail 1000 策略（保留关键上下文）
        int headLen = maxLen / 4;
        int tailLen = maxLen * 3 / 4;
        
        return input[..headLen] + "\n... [TRUNCATED] ...\n" + input[^tailLen..];
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
}
