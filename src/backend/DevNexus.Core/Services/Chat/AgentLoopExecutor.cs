using DevNexus.Core.Abstractions;
using DevNexus.Core.Models.Evaluation;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Agent Loop 执行器 - 轻量级评估-修复循环
/// </summary>
public class AgentLoopExecutor
{
    private readonly IRuleResponseEvaluator _ruleEvaluator;
    private readonly ILlmResponseEvaluator _llmEvaluator;
    private readonly IRepairContextBuilder _repairBuilder;
    private readonly ILogger<AgentLoopExecutor> _logger;

    public AgentLoopExecutor(
        IRuleResponseEvaluator ruleEvaluator,
        ILlmResponseEvaluator llmEvaluator,
        IRepairContextBuilder repairBuilder,
        ILogger<AgentLoopExecutor> logger)
    {
        _ruleEvaluator = ruleEvaluator;
        _llmEvaluator = llmEvaluator;
        _repairBuilder = repairBuilder;
        _logger = logger;
    }

    /// <summary>
    /// 评估响应并决定是否需要修复
    /// </summary>
    public async Task<(bool needsRepair, string? repairPrompt)> EvaluateAndBuildRepairAsync(
        string userGoal,
        string result,
        List<ToolExecutionRecord> toolRecords,
        int attempt,
        Guid providerId,
        CancellationToken cancellationToken)
    {
        var normalizedToolRecords = ToolExecutionRecordNormalizer.Normalize(toolRecords);

        // 无工具调用，直接通过
        if (normalizedToolRecords.Count == 0)
        {
            return (false, null);
        }

        // 选择评估器：
        // 1. 仅当工具执行整体成功时，优先用规则评估做低成本快筛；
        // 2. 一旦出现工具失败，立即切到 LLM 评估，由模型判断是否需要人工介入/补充前置条件。
        var hasToolFailures = normalizedToolRecords.Any(record => !record.Success);
        var useRuleEvaluator = !hasToolFailures && attempt < 2;
        var evaluator = useRuleEvaluator
            ? (IResponseEvaluator)_ruleEvaluator
            : _llmEvaluator;

        _logger.LogInformation(
            "[AgentLoop] Evaluator selected | Attempt={Attempt} Evaluator={Evaluator}",
            attempt + 1,
            useRuleEvaluator ? "RuleBasedResponseEvaluator" : "LlmResponseEvaluator");

        await ThinkingContext.EmitAsync("⚙️ 正在评估响应质量...");

        var evaluation = await evaluator.EvaluateAsync(new EvaluationContext
        {
            Goal = userGoal,
            Result = result,
            ToolRecords = normalizedToolRecords,
            Attempt = attempt,
            ProviderId = providerId
        }, cancellationToken);

        _logger.LogInformation(
            "[AgentLoop] 评估完成 | Attempt={Attempt} Passed={Passed} Score={Score:F1}",
            attempt + 1, evaluation.Passed, evaluation.Score);

        await ThinkingContext.EmitAsync(
            evaluation.Passed
                ? $"✅ 评估通过 ({evaluation.Score:F0}/100)"
                : $"⚠️ 评估未通过 ({evaluation.Score:F0}/100): {evaluation.Feedback}");

        if (evaluation.Passed || !evaluation.CanRepair)
        {
            return (false, null);
        }

        // 构建修复提示
        var repairPrompt = _repairBuilder.Build(
            new EvaluationContext
            {
                Goal = userGoal,
                Result = result,
                ToolRecords = normalizedToolRecords,
                Attempt = attempt
            },
            evaluation);

        return (true, repairPrompt);
    }
}
