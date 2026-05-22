using DevNexus.Shared.Enums;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// Agent 单轮事件 DTO。
/// </summary>
public sealed class AgentTurnEventDto
{
    /// <summary>
    /// 轮次标识。
    /// </summary>
    public Guid TurnId { get; set; }

    /// <summary>
    /// 事件顺序。
    /// </summary>
    public int Sequence { get; set; }

    /// <summary>
    /// 事件类型。
    /// </summary>
    public AgentTurnEventKind Kind { get; set; }

    /// <summary>
    /// 工具调用标识。
    /// </summary>
    public Guid? ToolCallId { get; set; }

    /// <summary>
    /// 工具名称。
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// 事件标题。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 事件摘要。
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 建议动作。
    /// </summary>
    public ToolSuggestedAction SuggestedAction { get; set; }
}

/// <summary>
/// Agent 单轮事件批次 DTO。
/// </summary>
public sealed class AgentTurnEventsUpdatedDto
{
    /// <summary>
    /// 轮次标识。
    /// </summary>
    public Guid TurnId { get; set; }

    /// <summary>
    /// 单轮事件列表。
    /// </summary>
    public List<AgentTurnEventDto> Events { get; set; } = new();

    /// <summary>
    /// 事件总数。
    /// </summary>
    public int EventCount { get; set; }

    /// <summary>
    /// 失败事件数。
    /// </summary>
    public int FailedEventCount { get; set; }

    /// <summary>
    /// 事件批次摘要指纹。
    /// </summary>
    public string EventBatchHash { get; set; } = string.Empty;

    /// <summary>
    /// 批次诊断摘要。
    /// </summary>
    public AgentTurnEventBatchDiagnosticsDto BatchDiagnostics { get; set; } = new();
}
