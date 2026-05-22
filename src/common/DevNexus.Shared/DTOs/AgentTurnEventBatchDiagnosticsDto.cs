using DevNexus.Shared.Enums;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 工具执行事件批次诊断 DTO。
/// </summary>
public sealed class AgentTurnEventBatchDiagnosticsDto
{
    /// <summary>
    /// 是否存在失败事件。
    /// </summary>
    public bool HasFailures { get; set; }

    /// <summary>
    /// 首个事件序号。
    /// </summary>
    public int FirstSequence { get; set; }

    /// <summary>
    /// 最后事件序号。
    /// </summary>
    public int LastSequence { get; set; }

    /// <summary>
    /// 成功完成的事件数。
    /// </summary>
    public int CompletedEventCount { get; set; }

    /// <summary>
    /// 失败事件数。
    /// </summary>
    public int FailedEventCount { get; set; }

    /// <summary>
    /// 去重后的工具数量。
    /// </summary>
    public int UniqueToolCount { get; set; }

    /// <summary>
    /// 批次内工具执行总耗时（毫秒）。
    /// </summary>
    public long TotalDurationMs { get; set; }

    /// <summary>
    /// 本批次中耗时最长的工具名称。
    /// </summary>
    public string? SlowestToolName { get; set; }

    /// <summary>
    /// 本批次中耗时最长的工具耗时（毫秒）。
    /// </summary>
    public long SlowestDurationMs { get; set; }

    /// <summary>
    /// 首个失败事件序号；无失败时为 0。
    /// </summary>
    public int FirstFailedSequence { get; set; }

    /// <summary>
    /// 首个失败工具名称；无失败时为空。
    /// </summary>
    public string? FirstFailedToolName { get; set; }

    /// <summary>
    /// 首个失败事件摘要；无失败时为空。
    /// </summary>
    public string? FirstFailureSummary { get; set; }

    /// <summary>
    /// 批次级优先建议动作。
    /// </summary>
    public ToolSuggestedAction PrimarySuggestedAction { get; set; }

    /// <summary>
    /// 批次级优先建议动作的展示文本。
    /// </summary>
    public string PrimarySuggestedActionText { get; set; } = string.Empty;
}
