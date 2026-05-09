using DevNexus.Shared.DTOs.Swarm;

namespace DevNexus.Core.Services.Swarm.Planning;

/// <summary>
/// 上下文工作包映射器。
/// </summary>
public static class ContextWorkPackageMapper
{
    /// <summary>
    /// 将领域工作包映射为共享 DTO。
    /// </summary>
    public static ContextWorkPackageDto ToDto(DevNexus.Domain.Models.Swarm.ContextWorkPackage package)
    {
        return new ContextWorkPackageDto
        {
            Id = package.Id,
            Title = package.Title,
            Objective = package.Objective,
            ContextType = package.ContextType.ToString(),
            Status = package.Status.ToString(),
            ExecutionStrategy = package.ExecutionStrategy.ToString(),
            Dependencies = package.Dependencies.Select(dependency => dependency.SourcePackageId).Distinct().ToList(),
            Result = package.Result,
            FailureReason = package.FailureReason,
            RiskLevel = package.RiskLevel,
            ExecutorName = package.ExecutorName,
            CommandLine = package.CommandLine,
            WorkingDirectory = package.WorkingDirectory,
            ExecutionReportArtifactId = package.ExecutionReportArtifactId,
            UpdatedAt = package.UpdatedAt,
            CanRetry = package.Status == DevNexus.Domain.Enums.SwarmPackageStatus.Failed,
            OwnedFiles = package.OwnedFiles.ToList(),
            OwnedSymbols = package.OwnedSymbols.ToList()
        };
    }
}
