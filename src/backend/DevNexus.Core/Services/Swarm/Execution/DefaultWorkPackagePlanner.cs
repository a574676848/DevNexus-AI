using DevNexus.Domain.Models.Swarm;
using DevNexus.Domain.Enums;

namespace DevNexus.Core.Services.Swarm.Execution;

/// <summary>
/// 默认工作包规划器。
/// </summary>
public class DefaultWorkPackagePlanner : IWorkPackagePlanner
{
    /// <summary>
    /// 将工作包组装为执行计划。
    /// </summary>
    public Task<SwarmExecutionPlan> PlanAsync(
        string sessionId,
        IReadOnlyList<ContextWorkPackage> packages,
        CancellationToken cancellationToken = default)
    {
        var plannedPackages = packages.Select(ClonePackage).ToList();
        BuildContracts(plannedPackages);
        AssignExecutionHints(plannedPackages);

        var plan = new SwarmExecutionPlan
        {
            SessionId = sessionId,
            Summary = $"已生成 {plannedPackages.Count} 个上下文工作包，并完成基础契约规划。",
            Packages = plannedPackages
        };

        return Task.FromResult(plan);
    }

    /// <summary>
    /// 构建工作包之间的基础输入输出契约。
    /// </summary>
    private static void BuildContracts(List<ContextWorkPackage> packages)
    {
        foreach (var package in packages)
        {
            EnsureOutputContract(package);
        }

        foreach (var package in packages)
        {
            foreach (var dependency in package.Dependencies)
            {
                var upstreamPackage = packages.FirstOrDefault(item => item.Id == dependency.SourcePackageId);
                if (upstreamPackage == null)
                {
                    continue;
                }

                var contractName = BuildDependencyContractName(upstreamPackage, package);
                if (package.InputContracts.Any(contract => string.Equals(contract.Name, contractName, StringComparison.Ordinal)))
                {
                    continue;
                }

                package.InputContracts.Add(new ContextContract
                {
                    Name = contractName,
                    Description = dependency.Reason,
                    Schema = BuildSchema(upstreamPackage.ContextType),
                    Required = true,
                    ProducerPackageId = upstreamPackage.Id,
                    ConsumerPackageId = package.Id,
                    ContextType = upstreamPackage.ContextType
                });
            }
        }
    }

    private static void AssignExecutionHints(List<ContextWorkPackage> packages)
    {
        foreach (var package in packages)
        {
            if (package.ContextType == SwarmContextType.Codebase && package.OwnedFiles.Count == 0)
            {
                package.OwnedFiles.Add("src/");
            }

            if (package.ContextType == SwarmContextType.ApiContract && package.OwnedSymbols.Count == 0)
            {
                package.OwnedSymbols.Add("Controller");
                package.OwnedSymbols.Add("Dto");
            }

            if (package.ContextType == SwarmContextType.Data && package.OwnedSymbols.Count == 0)
            {
                package.OwnedSymbols.Add("Entity");
                package.OwnedSymbols.Add("Migration");
            }
        }
    }

    /// <summary>
    /// 为工作包补齐默认输出契约。
    /// </summary>
    private static void EnsureOutputContract(ContextWorkPackage package)
    {
        if (package.OutputContracts.Count > 0)
        {
            return;
        }

        package.OutputContracts.Add(new ContextContract
        {
            Name = BuildOutputContractName(package),
            Description = $"输出 {package.ContextType} 上下文的结构化结果。",
            Schema = BuildSchema(package.ContextType),
            Required = true,
            ProducerPackageId = package.Id,
            ConsumerPackageId = null,
            ContextType = package.ContextType
        });
    }

    /// <summary>
    /// 构建输出契约名称。
    /// </summary>
    private static string BuildOutputContractName(ContextWorkPackage package)
    {
        return $"{package.ContextType}Output";
    }

    /// <summary>
    /// 构建依赖契约名称。
    /// </summary>
    private static string BuildDependencyContractName(ContextWorkPackage sourcePackage, ContextWorkPackage targetPackage)
    {
        return $"{sourcePackage.ContextType}To{targetPackage.ContextType}";
    }

    /// <summary>
    /// 根据上下文类型生成基础 Schema。
    /// </summary>
    private static string BuildSchema(SwarmContextType contextType)
    {
        return contextType switch
        {
            SwarmContextType.Task => """{ "objective": "string", "acceptanceCriteria": ["string"] }""",
            SwarmContextType.Codebase => """{ "files": ["string"], "symbols": ["string"], "changePlan": "string" }""",
            SwarmContextType.ApiContract => """{ "routes": ["string"], "request": "object", "response": "object" }""",
            SwarmContextType.Data => """{ "entities": ["string"], "migrations": ["string"], "queries": ["string"] }""",
            SwarmContextType.Frontend => """{ "components": ["string"], "states": ["string"], "uiNotes": "string" }""",
            SwarmContextType.Infrastructure => """{ "configs": ["string"], "deploymentNotes": "string", "risks": ["string"] }""",
            SwarmContextType.Evidence => """{ "sources": ["string"], "facts": ["string"], "confidence": "number" }""",
            _ => """{ "summary": "string" }"""
        };
    }

    /// <summary>
    /// 克隆工作包，避免直接修改上游集合。
    /// </summary>
    private static ContextWorkPackage ClonePackage(ContextWorkPackage package)
    {
        return new ContextWorkPackage
        {
            Id = package.Id,
            SessionId = package.SessionId,
            Title = package.Title,
            Objective = package.Objective,
            ContextType = package.ContextType,
            TaskContext = package.TaskContext,
            StateContext = package.StateContext,
            MemoryContext = package.MemoryContext,
            EvidenceContext = package.EvidenceContext,
            InputContracts = package.InputContracts.ToList(),
            OutputContracts = package.OutputContracts.ToList(),
            Dependencies = package.Dependencies.ToList(),
            VisibilityLevel = package.VisibilityLevel,
            ExecutionStrategy = package.ExecutionStrategy,
            RiskLevel = package.RiskLevel,
            CanRunInParallel = package.CanRunInParallel,
            OwnedFiles = package.OwnedFiles.ToList(),
            OwnedSymbols = package.OwnedSymbols.ToList(),
            Status = package.Status,
            Result = package.Result,
            Evaluation = package.Evaluation,
            FailureReason = package.FailureReason,
            ExecutorName = package.ExecutorName,
            CommandLine = package.CommandLine,
            WorkingDirectory = package.WorkingDirectory,
            ExecutionReportArtifactId = package.ExecutionReportArtifactId,
            CreatedAt = package.CreatedAt,
            UpdatedAt = package.UpdatedAt
        };
    }
}
