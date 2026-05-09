using DevNexus.Core.Abstractions;
using DevNexus.Core.Models.Evaluation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DevNexus.Infrastructure.Services.Evaluation;

/// <summary>
/// 基于规则的响应评估器（零额外 Token 消耗）
/// 遵循洋葱架构：具体实现在 Infrastructure 层
/// 
/// 设计哲学：
/// 对于单 Agent 全局启用场景，每次请求都走评估 —— 
/// 如果用 LLM 评估则成本翻倍不可接受。
/// 规则评估覆盖 80% 的明显失败场景，足以作为"第一道防线"。
/// </summary>
public class RuleBasedResponseEvaluator : IRuleResponseEvaluator
{
    private const double PassThreshold = 80.0;

    public Task<EvaluationResult> EvaluateAsync(
        EvaluationContext context, CancellationToken ct = default)
    {
        double score = 100.0;
        var suggestions = new List<string>();
        string feedback = "";
        
        var toolRecords = context.ToolRecords ?? new();
        
        // 规则 1：所有工具调用全部失败（严重问题）
        if (toolRecords.Count > 0 && toolRecords.All(t => !t.Success))
        {
            score -= 60;
            feedback = "所有工具执行均失败";
            suggestions.Add("分析每个工具的错误信息，尝试不同的命令或参数");
            suggestions.Add("如果是依赖缺失，先安装再重试");
        }
        
        // 规则 2：LLM 明确表示放弃
        var failureMarkers = new[] { 
            "无法完成", "我无法", "抱歉，我不能",
            "I cannot", "I'm unable", "failed to" 
        };
        if (toolRecords.Count > 0 && 
            failureMarkers.Any(m => context.Result.Contains(m, StringComparison.OrdinalIgnoreCase)))
        {
            score -= 40;
            feedback = "LLM 明确表示无法完成任务";
            suggestions.Add("尝试完全不同的实现方案");
        }
        
        // 规则 3：工具调用后响应过短（空洞回答）
        if (context.Result.Trim().Length < 50 && toolRecords.Count > 0)
        {
            score -= 30;
            feedback = "工具调用后的响应过短，可能未正确处理结果";
            suggestions.Add("仔细阅读工具返回的结果，给出详细的分析和方案");
        }
        
        // 规则 4：存在未处理的错误（工具报错但 LLM 没在回复中体现或解决）
        var unresolvedErrors = toolRecords
            .Where(t => !t.Success && 
                   !string.IsNullOrEmpty(t.ErrorSummary) &&
                   !context.Result.Contains(t.ErrorSummary!, StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        if (unresolvedErrors.Count > 0)
        {
            score -= 25;
            feedback = string.IsNullOrEmpty(feedback) 
                ? $"存在 {unresolvedErrors.Count} 个未处理的工具执行错误" 
                : feedback + $"; 另有 {unresolvedErrors.Count} 个未处理错误";
            
            foreach (var err in unresolvedErrors)
            {
                suggestions.Add($"处理 {err.ToolName} 的错误: {err.ErrorSummary}");
            }
        }
        
        // 规则 5：无工具调用且无修复 → 直接通过（简单问答场景）
        if (toolRecords.Count == 0)
        {
            score = 100;
            feedback = "";
            suggestions.Clear();
        }

        bool passed = score >= PassThreshold;
        var result = new EvaluationResult
        {
            Passed = passed,
            Score = Math.Max(0, score),
            CorrectnessScore = score,
            CompletenessScore = score,
            QualityScore = score,
            EfficiencyScore = score,
            Feedback = feedback,
            CanRepair = !passed && score >= 30, // 分数太低可能说明死循环或完全跑偏
            ImprovementSuggestions = suggestions
        };
        
        return Task.FromResult(result);
    }
}
