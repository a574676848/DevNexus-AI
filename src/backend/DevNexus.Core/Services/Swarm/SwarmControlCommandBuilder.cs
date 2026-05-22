using DevNexus.Domain.Entities;
using DevNexus.Shared.DTOs.Swarm;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Swarm;

/// <summary>
/// Swarm 控制命令载荷构建器。
/// </summary>
public static class SwarmControlCommandBuilder
{
    /// <summary>
    /// 构建控制命令结果。
    /// </summary>
    public static SwarmControlCommandDto Build(
        string sessionId,
        string command,
        IEnumerable<ContextWorkPackageRecord> packageRecords,
        bool isPaused,
        bool accepted = true,
        string? message = null)
    {
        var packages = packageRecords
            .OrderBy(package => package.CreatedAt)
            .Select(ToPackageDto)
            .ToList();

        return new SwarmControlCommandDto
        {
            SessionId = sessionId,
            Command = command,
            Accepted = accepted,
            Message = string.IsNullOrWhiteSpace(message)
                ? ResolveDefaultMessage(command, accepted)
                : message,
            StatusSummary = SwarmSessionStatusSummaryBuilder.Build(packages, isPaused)
        };
    }

    private static string ResolveDefaultMessage(string command, bool accepted)
    {
        if (!accepted)
        {
            return "Swarm 控制命令未生效。";
        }

        return command switch
        {
            "Paused" => "Swarm 已暂停。",
            "Resumed" => "Swarm 已继续。",
            "Aborted" => "Swarm 已中止。",
            "RetryStarted" => "工作包重试已开始。",
            "RetryCompleted" => "工作包重试已完成。",
            _ => "Swarm 控制命令已生效。"
        };
    }

    private static ContextWorkPackageDto ToPackageDto(ContextWorkPackageRecord package)
    {
        return new ContextWorkPackageDto
        {
            Id = package.TaskId,
            Title = package.Title,
            Objective = package.Description,
            ContextType = package.ContextType,
            Status = package.Status.ToString(),
            ExecutionStrategy = package.ExecutionStrategy,
            Dependencies = package.Dependencies,
            Result = package.Result,
            FailureReason = package.FailureReason,
            RiskLevel = 0,
            ExecutorName = package.ExecutorName,
            CommandLine = package.CommandLine,
            WorkingDirectory = package.WorkingDirectory,
            ExecutionReportArtifactId = package.ExecutionReportArtifactId,
            UpdatedAt = package.UpdatedAt,
            CanRetry = package.Status == SwarmTaskStatus.Failed,
            OwnedFiles = package.OwnedFiles,
            OwnedSymbols = package.OwnedSymbols
        };
    }
}
