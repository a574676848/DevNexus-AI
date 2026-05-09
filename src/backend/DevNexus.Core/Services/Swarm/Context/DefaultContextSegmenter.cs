using DevNexus.Domain.Models.Swarm;

namespace DevNexus.Core.Services.Swarm.Context;

/// <summary>
/// 默认上下文切分器。
/// </summary>
public class DefaultContextSegmenter : IContextSegmenter
{
    /// <summary>
    /// 将上下文草案切分为具备基础依赖关系的工作包列表。
    /// </summary>
    public Task<IReadOnlyList<ContextWorkPackage>> SegmentAsync(
        IReadOnlyList<ContextWorkPackage> draftPackages,
        CancellationToken cancellationToken = default)
    {
        if (draftPackages.Count == 0)
        {
            return Task.FromResult(draftPackages);
        }

        var orderedPackages = draftPackages
            .OrderBy(GetSortOrder)
            .Select(ClonePackage)
            .ToList();

        var taskRoot = orderedPackages.FirstOrDefault(package => package.ContextType == DevNexus.Domain.Enums.SwarmContextType.Task)
            ?? orderedPackages[0];

        foreach (var package in orderedPackages)
        {
            if (package.Id == taskRoot.Id)
            {
                continue;
            }

            package.Dependencies.Add(new ContextDependency
            {
                SourcePackageId = taskRoot.Id,
                TargetPackageId = package.Id,
                Reason = "该工作包依赖统一的任务目标与验收标准。"
            });
        }

        var evidencePackage = orderedPackages.FirstOrDefault(package => package.ContextType == DevNexus.Domain.Enums.SwarmContextType.Evidence);
        if (evidencePackage != null)
        {
            foreach (var package in orderedPackages.Where(package => package.Id != evidencePackage.Id && package.ContextType != DevNexus.Domain.Enums.SwarmContextType.Task))
            {
                package.Dependencies.Add(new ContextDependency
                {
                    SourcePackageId = evidencePackage.Id,
                    TargetPackageId = package.Id,
                    Reason = "该工作包需要共享证据与历史上下文。"
                });
            }
        }

        BindSpecializedDependencies(orderedPackages);
        AssignParallelEligibility(orderedPackages);

        return Task.FromResult<IReadOnlyList<ContextWorkPackage>>(orderedPackages);
    }

    private static void BindSpecializedDependencies(List<ContextWorkPackage> packages)
    {
        var apiPackage = packages.FirstOrDefault(package => package.ContextType == DevNexus.Domain.Enums.SwarmContextType.ApiContract);
        var dataPackage = packages.FirstOrDefault(package => package.ContextType == DevNexus.Domain.Enums.SwarmContextType.Data);
        var codePackage = packages.FirstOrDefault(package => package.ContextType == DevNexus.Domain.Enums.SwarmContextType.Codebase);
        var frontendPackage = packages.FirstOrDefault(package => package.ContextType == DevNexus.Domain.Enums.SwarmContextType.Frontend);

        if (apiPackage != null && codePackage != null)
        {
            codePackage.Dependencies.Add(new ContextDependency
            {
                SourcePackageId = apiPackage.Id,
                TargetPackageId = codePackage.Id,
                Reason = "代码实现需要明确接口与契约边界。"
            });
        }

        if (dataPackage != null && codePackage != null)
        {
            codePackage.Dependencies.Add(new ContextDependency
            {
                SourcePackageId = dataPackage.Id,
                TargetPackageId = codePackage.Id,
                Reason = "代码实现需要明确实体与数据结构。"
            });
        }

        if (frontendPackage != null && apiPackage != null)
        {
            frontendPackage.Dependencies.Add(new ContextDependency
            {
                SourcePackageId = apiPackage.Id,
                TargetPackageId = frontendPackage.Id,
                Reason = "前端交互依赖接口契约与返回结构。"
            });
        }
    }

    private static void AssignParallelEligibility(List<ContextWorkPackage> packages)
    {
        foreach (var package in packages)
        {
            package.CanRunInParallel = package.Dependencies.Count <= 1
                && package.ContextType is not DevNexus.Domain.Enums.SwarmContextType.Task
                && package.VisibilityLevel != DevNexus.Domain.Enums.SwarmVisibilityLevel.PackageOnly;
        }
    }

    /// <summary>
    /// 获取默认排序权重。
    /// </summary>
    private static int GetSortOrder(ContextWorkPackage package)
    {
        return package.ContextType switch
        {
            DevNexus.Domain.Enums.SwarmContextType.Task => 0,
            DevNexus.Domain.Enums.SwarmContextType.Evidence => 1,
            DevNexus.Domain.Enums.SwarmContextType.ApiContract => 2,
            DevNexus.Domain.Enums.SwarmContextType.Data => 3,
            DevNexus.Domain.Enums.SwarmContextType.Codebase => 4,
            DevNexus.Domain.Enums.SwarmContextType.Frontend => 5,
            DevNexus.Domain.Enums.SwarmContextType.Infrastructure => 6,
            _ => 9
        };
    }

    /// <summary>
    /// 克隆工作包，避免直接修改上游草案对象。
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
