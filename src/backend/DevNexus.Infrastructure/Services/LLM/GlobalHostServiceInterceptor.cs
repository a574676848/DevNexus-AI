using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using DevNexus.Core.Abstractions;
using System.Text.Json;

namespace DevNexus.Infrastructure.Services.LLM;

/// <summary>
/// 全局 HostService 安全拦截器
/// 拦截高危 HostService 操作（写入/执行命令），并弹窗让用户二次确认
/// 实现 IAutoFunctionInvocationFilter 接口，在 SK 自动调用函数前拦截
/// </summary>
public class GlobalHostServiceInterceptor : IAutoFunctionInvocationFilter
{
    private readonly IConfirmationService _confirmationService;
    private readonly ILogger<GlobalHostServiceInterceptor> _logger;
    private readonly string _sessionId;

    /// <summary>
    /// 高危操作方法名（写入文件、执行命令等）
    /// </summary>
    private static readonly HashSet<string> DangerousMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "WriteFileTextAsync",
        "ExecuteCommandAsync",
        "DeleteFile",
        "MoveFile",
        "CopyFile",
        "ApplyDiffAsync"
    };

    /// <summary>
    /// 只读操作方法名（无需拦截）
    /// </summary>
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "ReadFileTextAsync",
        "ListDirectoryAsync",
        "GetWorkingDirectory",
        "FileExists",
        "GetFileInfo",
        "SearchInFilesAsync"
    };

    public GlobalHostServiceInterceptor(
        IConfirmationService confirmationService,
        ILogger<GlobalHostServiceInterceptor> logger,
        string sessionId = "")
    {
        _confirmationService = confirmationService;
        _logger = logger;
        _sessionId = sessionId;
    }

    /// <summary>
    /// 在自动函数调用前执行拦截逻辑
    /// </summary>
    public async Task OnAutoFunctionInvocationAsync(
        AutoFunctionInvocationContext context,
        Func<AutoFunctionInvocationContext, Task> next)
    {
        // 仅拦截 HostService Plugin 的调用
        if (!string.Equals(context.Function.PluginName, "HostService", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var functionName = context.Function.Name;

        // 只读操作直接放行
        if (SafeMethods.Contains(functionName))
        {
            _logger.LogDebug("[Skill.Security] HostService 只读操作放行 | Method={Method}", functionName);
            await next(context);
            return;
        }

        // 高危操作：请求用户确认
        if (DangerousMethods.Contains(functionName))
        {
            if (string.IsNullOrEmpty(_sessionId))
            {
                _logger.LogWarning("[Skill.Security] 缺少 SessionId，无法发起确认请求 | Method={Method}", functionName);
                context.Result = new FunctionResult(
                    context.Function,
                    "操作被安全策略拦截：无法验证会话安全性，操作被拒绝。");
                return;
            }

            var payload = context.Arguments != null ? JsonSerializer.Serialize(context.Arguments) : "{}";
            
            _logger.LogInformation("[Skill.Security] 拦截高危操作并请求用户确认 | Session={Session} Method={Method}", _sessionId, functionName);
            
            var approved = await _confirmationService.RequestConfirmationAsync(
                _sessionId,
                functionName,
                payload,
                context.CancellationToken);

            if (!approved)
            {
                _logger.LogWarning("[Skill.Security] 用户拒绝了高危操作 | Method={Method}", functionName);
                context.Result = new FunctionResult(
                    context.Function,
                    "The user rejected this operation. Please do not attempt it again and explain to the user.");
                return;
            }

            _logger.LogInformation("[Skill.Security] 用户批准了高危操作 | Method={Method}", functionName);
            await next(context);
            return;
        }

        // 未知方法：默认放行但记录
        _logger.LogInformation("[Skill.Security] HostService 未知操作 | Method={Method}", functionName);
        await next(context);
    }
}
