using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DevNexus.Core.Abstractions;
using DevNexus.Core.Models.Evaluation;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace DevNexus.Infrastructure.Services.Evaluation;

/// <summary>
/// 统一修复上下文构建器
/// 遵循洋葱架构：具体实现在 Infrastructure 层
/// </summary>
public class RepairContextBuilder : IRepairContextBuilder
{
    private readonly ILogger<RepairContextBuilder> _logger;

    public RepairContextBuilder(ILogger<RepairContextBuilder> logger)
    {
        _logger = logger;
    }

    public string Build(EvaluationContext context, EvaluationResult evaluation)
    {
        _logger.LogInformation(
            "构建修复上下文 (评分: {Score:F1}, 第 {Attempt} 次重试)",
            evaluation.Score, context.Attempt);

        var sb = new StringBuilder();
        sb.AppendLine($"## ⚠️ 修复指令 (第 {context.Attempt} 次重试)");
        sb.AppendLine();
        sb.AppendLine($"上一次执行结果未通过质量评估（分数: {evaluation.Score:F1}/100）。");
        sb.AppendLine();
        
        if (!string.IsNullOrEmpty(evaluation.Feedback))
        {
            sb.AppendLine("### 评估反馈");
            sb.AppendLine(evaluation.Feedback);
            sb.AppendLine();
        }

        sb.AppendLine("### 各维度分数");
        sb.AppendLine($"- 正确性: {evaluation.CorrectnessScore:F0}/100");
        sb.AppendLine($"- 完整性: {evaluation.CompletenessScore:F0}/100");
        sb.AppendLine($"- 质量: {evaluation.QualityScore:F0}/100");
        sb.AppendLine($"- 效率: {evaluation.EfficiencyScore:F0}/100");
        sb.AppendLine();

        if (evaluation.ImprovementSuggestions.Count > 0)
        {
            sb.AppendLine("### 改进建议");
            for (int i = 0; i < evaluation.ImprovementSuggestions.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {evaluation.ImprovementSuggestions[i]}");
            }
            sb.AppendLine();
        }

        // 工具执行错误摘要注入
        var failedTools = context.ToolRecords?.Where(t => !t.Success).ToList();
        if (failedTools?.Count > 0)
        {
            sb.AppendLine("### 失败的工具调用记录");
            foreach (var tool in failedTools)
            {
                sb.AppendLine($"- **{tool.ToolName}**");
                sb.AppendLine($"  failureReason: {tool.FailureReason.ToWireValue()}");
                sb.AppendLine($"  retryable: {tool.Retryable}");
                sb.AppendLine($"  requiresHumanIntervention: {tool.RequiresHumanIntervention}");
                sb.AppendLine($"  shouldFallback: {tool.ShouldFallback}");
                sb.AppendLine($"  shouldRotateCredential: {tool.ShouldRotateCredential}");
                sb.AppendLine($"  suggestedAction: {tool.SuggestedAction.ToWireValue()}");
                if (!string.IsNullOrWhiteSpace(tool.RequestedUserInputKind))
                {
                    sb.AppendLine($"  requestedUserInputKind: {tool.RequestedUserInputKind}");
                }
                if (!string.IsNullOrWhiteSpace(tool.RequestedUserInputLabel))
                {
                    sb.AppendLine($"  requestedUserInputLabel: {tool.RequestedUserInputLabel}");
                }
                if (!string.IsNullOrWhiteSpace(tool.UserMessage))
                {
                    sb.AppendLine($"  userMessage: {tool.UserMessage}");
                }
                sb.AppendLine($"  error: {tool.ErrorSummary ?? "执行失败"}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("### 你之前的输出（摘要）");
        sb.AppendLine("```");
        sb.AppendLine(context.Result.Length > 2000 
            ? context.Result[..2000] + "\n... (已截断)" 
            : context.Result);
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("### 要求");
        sb.AppendLine("请基于以上反馈 **重新执行** 任务。确保：");
        sb.AppendLine("1. 针对评估中被扣分的问题进行针对性修复");
        sb.AppendLine("2. 不要简单重复上一次的输出");
        sb.AppendLine("3. 如果工具调用失败，分析错误根因并尝试不同方案");
        sb.AppendLine("4. 输出完整的修复后结果");
        sb.AppendLine();
        sb.AppendLine("### 自主决策");
        sb.AppendLine("如果你认为问题**无法通过重试解决**（例如：缺少必要权限、环境根本不支持、用户需求本身不合理），");
        sb.AppendLine("请在回复的**最后一行**添加标记：`[AGENT_LOOP_STOP]`");
        sb.AppendLine("这将停止自动重试，并将当前结果返回给用户。");

        return sb.ToString();
    }
}
