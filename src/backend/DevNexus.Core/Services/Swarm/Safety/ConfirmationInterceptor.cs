using System;
using System.Threading;
using System.Threading.Tasks;
using DevNexus.Core.Abstractions;
using DevNexus.Shared.Constants;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace DevNexus.Core.Services.Swarm.Safety;

/// <summary>
/// 安全确认拦截器 - 拦截所有工具调用，如果是高风险操作则请求用户确认
/// </summary>
public class ConfirmationInterceptor : IAutoFunctionInvocationFilter
{
    private readonly IConfirmationService _confirmationService;
    private readonly string _sessionId;
    private readonly ILogger<ConfirmationInterceptor> _logger;

    public ConfirmationInterceptor(IConfirmationService confirmationService, ILogger<ConfirmationInterceptor> logger, string sessionId = "")
    {
        _confirmationService = confirmationService;
        _sessionId = sessionId;
        _logger = logger;
    }

    public IConfirmationService GetConfirmationService() => _confirmationService;

    public async Task OnAutoFunctionInvocationAsync(AutoFunctionInvocationContext context, Func<AutoFunctionInvocationContext, Task> next)
    {
        var functionName = context.Function.Name;
        _logger.LogDebug("Intercepting function call: {Name}", functionName);

        //Check if operation is high risk
        if (HighRiskOperations.All.Contains(functionName))
        {
            _logger.LogWarning("High-risk operation detected: {Op}. Requesting confirmation...", functionName);

            try
            {
                var approved = await _confirmationService.RequestConfirmationAsync(
                    _sessionId, 
                    functionName, 
                    System.Text.Json.JsonSerializer.Serialize(context.Arguments), 
                    context.CancellationToken);

                if (!approved)
                {
                    _logger.LogWarning("User REJECTED operation {Op}", functionName);
                    // 抛出异常以终止该 Function 调用链，并让 Agent 感知到被拒绝
                    throw new OperationCanceledException($"User rejected high-risk operation: {functionName}");
                }
                
                _logger.LogInformation("User APPROVED operation {Op}", functionName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during confirmation process for {Op}", functionName);
                throw; // Re-throw to stop execution
            }
        }

        // 继续执行
        await next(context);
    }
}
