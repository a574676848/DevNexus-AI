using DevNexus.Domain.Models.Swarm;
using Microsoft.Extensions.Logging;
using System.Text;

namespace DevNexus.Core.Services.Swarm.Evaluation;

/// <summary>
/// 默认上下文评估服务。
/// </summary>
public class DefaultContextEvaluationService : IContextEvaluationService
{
    private readonly ILogger<DefaultContextEvaluationService> _logger;

    /// <summary>
    /// 初始化评估服务。
    /// </summary>
    public DefaultContextEvaluationService(ILogger<DefaultContextEvaluationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 为当前计划写入默认评估结果。
    /// </summary>
    public Task EvaluateAsync(
        SwarmExecutionPlan plan,
        CancellationToken cancellationToken = default)
    {
        foreach (var package in plan.Packages)
        {
            package.Evaluation = BuildEvaluation(package);
        }

        _logger.LogInformation(
            "Swarm 工作包评估完成 | SessionId={SessionId} PackageCount={PackageCount}",
            plan.SessionId,
            plan.Packages.Count);

        return Task.CompletedTask;
    }

    private static string BuildEvaluation(ContextWorkPackage package)
    {
        var score = CalculateScore(package);
        var suggestion = GetSuggestion(package);
        var builder = new StringBuilder();
        builder.Append($"评分：{score:0}/100");
        builder.Append(" | ");
        builder.Append(package.Status == DevNexus.Domain.Enums.SwarmPackageStatus.Completed ? "结果可用" : "需要关注");
        builder.Append(" | ");
        builder.Append(suggestion);
        return builder.ToString();
    }

    private static double CalculateScore(ContextWorkPackage package)
    {
        var score = 100.0;
        if (string.IsNullOrWhiteSpace(package.Result))
        {
            score -= 35;
        }

        if (!string.IsNullOrWhiteSpace(package.FailureReason))
        {
            score -= 30;
        }

        score -= Math.Min(package.RiskLevel * 2, 20);
        score -= package.Dependencies.Count * 3;

        return Math.Clamp(score, 0, 100);
    }

    private static string GetSuggestion(ContextWorkPackage package)
    {
        if (!string.IsNullOrWhiteSpace(package.FailureReason))
        {
            return "建议优先查看失败原因、执行报告和关联终端。";
        }

        if (string.IsNullOrWhiteSpace(package.Result))
        {
            return "建议补充结果摘要和输出契约检查。";
        }

        if (package.Dependencies.Count > 2)
        {
            return "建议复核依赖链，避免后续工作包被隐式阻塞。";
        }

        return "当前工作包评估通过，可进入下游消费。";
    }
}
