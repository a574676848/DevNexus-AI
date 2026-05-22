using DevNexus.Shared.Enums;

namespace DevNexus.Core.Models.Evaluation;

/// <summary>
/// 工具执行记录（内存对象，不持久化到数据库）
/// 用于评估引擎的质量判断
/// </summary>
public record ToolExecutionRecord
{
    public Guid? ToolCallId { get; init; }
    public string ToolName { get; init; } = string.Empty;      // "HostService.ExecuteCommandAsync"
    public string Arguments { get; init; } = string.Empty;     // { command: "dotnet build", ... }
    public bool Success { get; init; }
    public ToolFailureReason FailureReason { get; init; } = ToolFailureReason.None;
    public bool Retryable { get; init; }
    public bool RequiresHumanIntervention { get; init; }
    public bool ShouldFallback { get; init; }
    public bool ShouldRotateCredential { get; init; }
    public ToolSuggestedAction SuggestedAction { get; init; } = ToolSuggestedAction.None;
    public string? UserMessage { get; init; }
    public string? RequestedUserInputKind { get; init; }
    public string? RequestedUserInputLabel { get; init; }
    public string? Output { get; init; }             // stdout (截断到 2000 字符)
    public string? ErrorMessage { get; init; }       // stderr 完整信息
    public string? ErrorSummary { get; init; }       // stderr 截断摘要 (≤200 字符)
    public int ExitCode { get; init; }
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// 评估结果数据结构
/// 从 Swarm 迁移到全局共享位置
/// </summary>
public class EvaluationResult
{
    public bool Passed { get; set; }
    public double Score { get; set; }
    public double CorrectnessScore { get; set; }
    public double CompletenessScore { get; set; }
    public double QualityScore { get; set; }
    public double EfficiencyScore { get; set; }
    public string Feedback { get; set; } = string.Empty;
    public bool CanRepair { get; set; }
    public List<string> ImprovementSuggestions { get; set; } = new();
}

/// <summary>
/// 统一评估上下文，兼容上下文工作包执行与单 Agent ChatRequest
/// </summary>
public record EvaluationContext
{
    /// <summary>
    /// 任务/用户目标描述
    /// </summary>
    public string Goal { get; init; } = string.Empty;

    /// <summary>
    /// LLM/Agent 的输出结果
    /// </summary>
    public string Result { get; init; } = string.Empty;

    /// <summary>
    /// 预期输出格式/Schema（可选）
    /// </summary>
    public string? ExpectedOutputSchema { get; init; }

    /// <summary>
    /// 工具执行记录
    /// </summary>
    public List<ToolExecutionRecord>? ToolRecords { get; init; }

    /// <summary>
    /// 当前重试次数
    /// </summary>
    public int Attempt { get; init; }

    /// <summary>
    /// 角色描述
    /// </summary>
    public string? Role { get; init; }

    /// <summary>
    /// LLM 供应商 ID（解耦 Core 对 Infrastructure 的类型转换依赖）
    /// </summary>
    public Guid? ProviderId { get; init; }
}
