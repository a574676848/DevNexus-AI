using DevNexus.Core.Services.Swarm.Analysis;
using DevNexus.Domain.Enums;
using DevNexus.Domain.Models.Swarm;
using System.Text.RegularExpressions;
using System.Text;

namespace DevNexus.Core.Services.Swarm.Context;

/// <summary>
/// 默认上下文分析器。
/// </summary>
public class DefaultContextAnalyzer : IContextAnalyzer
{
    private static readonly Regex FrontendRegex = new("(ui|ux|页面|前端|组件|样式|交互)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ApiRegex = new("(api|接口|controller|endpoint|swagger|contract)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DataRegex = new("(sql|db|database|migration|schema|表|字段|数据库)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex InfraRegex = new("(deploy|infra|docker|k8s|日志|监控|配置|发布)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CodeRegex = new("(code|refactor|重构|实现|开发|修复|bug|代码)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// 分析用户请求并生成初始上下文工作包草案。
    /// </summary>
    public Task<IReadOnlyList<ContextWorkPackage>> AnalyzeAsync(
        string userRequest,
        string sessionId,
        ComplexityVector complexity,
        CancellationToken cancellationToken = default)
    {
        var packages = new List<ContextWorkPackage>();
        var normalizedRequest = userRequest.Trim();
        var packageIndex = 1;

        packages.Add(CreatePackage(
            sessionId,
            packageIndex++,
            "任务目标与验收",
            normalizedRequest,
            SwarmContextType.Task,
            complexity,
            canRunInParallel: false));

        if (ShouldCreateCodebasePackage(complexity, normalizedRequest))
        {
            packages.Add(CreatePackage(
                sessionId,
                packageIndex++,
                "代码实现上下文",
                normalizedRequest,
                SwarmContextType.Codebase,
                complexity,
                canRunInParallel: complexity.TaskScale >= 5));
        }

        if (ApiRegex.IsMatch(normalizedRequest))
        {
            packages.Add(CreatePackage(
                sessionId,
                packageIndex++,
                "接口契约上下文",
                normalizedRequest,
                SwarmContextType.ApiContract,
                complexity,
                canRunInParallel: true));
        }

        if (DataRegex.IsMatch(normalizedRequest))
        {
            packages.Add(CreatePackage(
                sessionId,
                packageIndex++,
                "数据模型上下文",
                normalizedRequest,
                SwarmContextType.Data,
                complexity,
                canRunInParallel: complexity.RiskLevel < 8));
        }

        if (FrontendRegex.IsMatch(normalizedRequest))
        {
            packages.Add(CreatePackage(
                sessionId,
                packageIndex++,
                "前端交互上下文",
                normalizedRequest,
                SwarmContextType.Frontend,
                complexity,
                canRunInParallel: true));
        }

        if (InfraRegex.IsMatch(normalizedRequest))
        {
            packages.Add(CreatePackage(
                sessionId,
                packageIndex++,
                "基础设施上下文",
                normalizedRequest,
                SwarmContextType.Infrastructure,
                complexity,
                canRunInParallel: false));
        }

        // 当请求明确依赖文档、仓库或历史经验时，补一层证据上下文，避免所有工作包都直接继承整段原始请求。
        if (complexity.ContextDepth >= 5 || complexity.SemanticEntropy >= 0.45)
        {
            packages.Add(CreatePackage(
                sessionId,
                packageIndex,
                "证据与历史上下文",
                normalizedRequest,
                SwarmContextType.Evidence,
                complexity,
                canRunInParallel: true));
        }

        EnrichContextPackages(packages, normalizedRequest, complexity);

        return Task.FromResult<IReadOnlyList<ContextWorkPackage>>(packages);
    }

    /// <summary>
    /// 创建默认上下文工作包。
    /// </summary>
    private static ContextWorkPackage CreatePackage(
        string sessionId,
        int packageIndex,
        string title,
        string objective,
        SwarmContextType contextType,
        ComplexityVector complexity,
        bool canRunInParallel)
    {
        return new ContextWorkPackage
        {
            Id = $"pkg-{packageIndex}",
            SessionId = sessionId,
            Title = title,
            Objective = objective,
            ContextType = contextType,
            TaskContext = objective,
            StateContext = $"复杂度评分：{complexity.CompositeScore:F1} | 风险：{complexity.RiskLevel:F1}",
            MemoryContext = complexity.ContextDepth >= 5 ? "该任务对历史上下文有明显依赖。" : string.Empty,
            EvidenceContext = BuildEvidenceHint(contextType),
            RiskLevel = complexity.RiskLevel,
            CanRunInParallel = canRunInParallel
        };
    }

    /// <summary>
    /// 判断是否需要生成代码实现上下文。
    /// </summary>
    private static bool ShouldCreateCodebasePackage(ComplexityVector complexity, string request)
    {
        return complexity.PrimaryDomain == DomainType.Coding
            || complexity.TaskScale >= 4
            || CodeRegex.IsMatch(request);
    }

    /// <summary>
    /// 生成上下文对应的默认证据提示。
    /// </summary>
    private static string BuildEvidenceHint(SwarmContextType contextType)
    {
        return contextType switch
        {
            SwarmContextType.ApiContract => "优先关注 Controller、DTO、Swagger 与前后端契约。",
            SwarmContextType.Data => "优先关注实体、迁移、表结构与查询路径。",
            SwarmContextType.Frontend => "优先关注页面、组件、样式与交互状态。",
            SwarmContextType.Infrastructure => "优先关注配置、部署、日志、监控与运行环境。",
            SwarmContextType.Codebase => "优先关注实现文件、调用链与受影响符号。",
            SwarmContextType.Evidence => "优先汇总需求、历史经验、工具输出与外部资料。",
            _ => "优先提取任务目标、验收标准与直接约束。"
        };
    }

    /// <summary>
    /// 根据请求和复杂度补充工作包的上下文、风险与可见范围。
    /// </summary>
    private static void EnrichContextPackages(
        List<ContextWorkPackage> packages,
        string normalizedRequest,
        ComplexityVector complexity)
    {
        foreach (var package in packages)
        {
            package.VisibilityLevel = package.ContextType switch
            {
                SwarmContextType.Task => SwarmVisibilityLevel.SessionWide,
                SwarmContextType.Evidence => SwarmVisibilityLevel.DependencyScoped,
                _ => package.RiskLevel >= 7 ? SwarmVisibilityLevel.PackageOnly : SwarmVisibilityLevel.DependencyScoped
            };

            package.TaskContext = BuildTaskContext(normalizedRequest, complexity, package);
            package.StateContext = BuildStateContext(complexity, package);
            package.MemoryContext = BuildMemoryContext(normalizedRequest, complexity, package);
            package.EvidenceContext = $"{package.EvidenceContext}{Environment.NewLine}{BuildEvidenceContext(normalizedRequest, package)}".Trim();
            package.RiskLevel = AdjustRiskLevel(package.ContextType, complexity.RiskLevel);
        }
    }

    private static string BuildTaskContext(string request, ComplexityVector complexity, ContextWorkPackage package)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"用户请求：{request}");
        builder.AppendLine($"目标上下文：{package.ContextType}");
        builder.AppendLine($"复杂度：{complexity.CompositeScore:F1}");
        builder.AppendLine($"建议策略：{complexity.SuggestedMode}");
        return builder.ToString().Trim();
    }

    private static string BuildStateContext(ComplexityVector complexity, ContextWorkPackage package)
    {
        return $"复杂度评分：{complexity.CompositeScore:F1} | 风险：{package.RiskLevel:F1} | 并行：{(package.CanRunInParallel ? "允许" : "禁止")}";
    }

    private static string BuildMemoryContext(string request, ComplexityVector complexity, ContextWorkPackage package)
    {
        var hints = new List<string>();
        if (complexity.ContextDepth >= 5)
        {
            hints.Add("该任务需要复用会话历史与用户偏好。");
        }

        if (package.ContextType == SwarmContextType.Codebase && request.Contains("规范", StringComparison.OrdinalIgnoreCase))
        {
            hints.Add("实现阶段必须优先遵循项目既有规范与结构边界。");
        }

        if (package.ContextType == SwarmContextType.Frontend)
        {
            hints.Add("前端工作包必须保持内容优先、低噪工作台风格。");
        }

        return string.Join(Environment.NewLine, hints);
    }

    private static string BuildEvidenceContext(string request, ContextWorkPackage package)
    {
        var hints = new List<string>();

        if (request.Contains("docs", StringComparison.OrdinalIgnoreCase) || request.Contains("文档", StringComparison.OrdinalIgnoreCase))
        {
            hints.Add("需要引用仓库文档作为证据源。");
        }

        if (package.ContextType is SwarmContextType.Codebase or SwarmContextType.ApiContract)
        {
            hints.Add("需要结合代码实现、DTO、控制器和调用链。");
        }

        if (package.ContextType == SwarmContextType.Data)
        {
            hints.Add("需要结合实体、迁移与数据库结构。");
        }

        return string.Join(Environment.NewLine, hints);
    }

    private static double AdjustRiskLevel(SwarmContextType contextType, double baseRiskLevel)
    {
        var adjusted = contextType switch
        {
            SwarmContextType.Infrastructure => baseRiskLevel + 1.5,
            SwarmContextType.Data => baseRiskLevel + 1.0,
            SwarmContextType.ApiContract => baseRiskLevel + 0.5,
            _ => baseRiskLevel
        };

        return Math.Clamp(adjusted, 0, 10);
    }
}
