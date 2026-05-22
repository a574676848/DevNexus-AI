using DevNexus.Core.Models.Evaluation;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using System.Text;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Agent 单轮事件。
/// </summary>
public sealed record AgentTurnEvent
{
    /// <summary>
    /// 轮次标识。
    /// </summary>
    public Guid TurnId { get; init; }

    /// <summary>
    /// 事件顺序。
    /// </summary>
    public int Sequence { get; init; }

    /// <summary>
    /// 事件类型。
    /// </summary>
    public AgentTurnEventKind Kind { get; init; }

    /// <summary>
    /// 工具调用标识。
    /// </summary>
    public Guid? ToolCallId { get; init; }

    /// <summary>
    /// 工具名称。
    /// </summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>
    /// 事件标题。
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 事件摘要。
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 建议动作。
    /// </summary>
    public ToolSuggestedAction SuggestedAction { get; init; }
}

/// <summary>
/// Agent 单轮事件构建器。
/// </summary>
public static class AgentTurnEventBuilder
{
    private const int InitialSequence = 1;
    private const int NoFailedSequence = 0;

    /// <summary>
    /// 从工具执行记录构建稳定有序的单轮事件。
    /// </summary>
    public static IReadOnlyList<AgentTurnEvent> FromToolRecords(
        Guid turnId,
        IReadOnlyList<ToolExecutionRecord> toolRecords)
    {
        return toolRecords
            .Select((record, index) => BuildToolEvent(turnId, record, index + InitialSequence))
            .ToList();
    }

    /// <summary>
    /// 从工具执行记录构建单轮事件批次 DTO。
    /// </summary>
    public static AgentTurnEventsUpdatedDto BuildUpdatedDto(
        Guid turnId,
        IReadOnlyList<ToolExecutionRecord> toolRecords)
    {
        var events = FromToolRecords(turnId, toolRecords);

        return new AgentTurnEventsUpdatedDto
        {
            TurnId = turnId,
            Events = events.Select(ToDto).ToList(),
            EventCount = events.Count,
            FailedEventCount = events.Count(item => item.Kind == AgentTurnEventKind.ToolFailed),
            EventBatchHash = BuildBatchHash(events),
            BatchDiagnostics = BuildBatchDiagnostics(events, toolRecords)
        };
    }

    /// <summary>
    /// 构建事件批次诊断摘要。
    /// </summary>
    public static AgentTurnEventBatchDiagnosticsDto BuildBatchDiagnostics(IReadOnlyList<AgentTurnEvent> events)
    {
        return BuildBatchDiagnostics(events, []);
    }

    /// <summary>
    /// 构建事件批次诊断摘要。
    /// </summary>
    public static AgentTurnEventBatchDiagnosticsDto BuildBatchDiagnostics(
        IReadOnlyList<AgentTurnEvent> events,
        IReadOnlyList<ToolExecutionRecord> toolRecords)
    {
        var orderedEvents = events
            .OrderBy(turnEvent => turnEvent.Sequence)
            .ToList();
        var failedEvents = orderedEvents
            .Where(turnEvent => turnEvent.Kind == AgentTurnEventKind.ToolFailed)
            .ToList();
        var slowestTool = ResolveSlowestTool(toolRecords);
        var primaryAction = ResolvePrimaryAction(failedEvents, toolRecords);

        return new AgentTurnEventBatchDiagnosticsDto
        {
            HasFailures = failedEvents.Count > 0,
            FirstSequence = orderedEvents.FirstOrDefault()?.Sequence ?? 0,
            LastSequence = orderedEvents.LastOrDefault()?.Sequence ?? 0,
            CompletedEventCount = orderedEvents.Count(item => item.Kind == AgentTurnEventKind.ToolCompleted),
            FailedEventCount = failedEvents.Count,
            UniqueToolCount = orderedEvents
                .Select(item => item.ToolName.Trim())
                .Where(toolName => !string.IsNullOrWhiteSpace(toolName))
                .Distinct(StringComparer.Ordinal)
                .Count(),
            TotalDurationMs = CalculateTotalDurationMs(toolRecords),
            SlowestToolName = slowestTool.ToolName,
            SlowestDurationMs = slowestTool.DurationMs,
            FirstFailedSequence = failedEvents.FirstOrDefault()?.Sequence ?? NoFailedSequence,
            FirstFailedToolName = ResolveFirstFailedToolName(failedEvents),
            FirstFailureSummary = ResolveFirstFailureSummary(failedEvents),
            PrimarySuggestedAction = primaryAction,
            PrimarySuggestedActionText = primaryAction.ToDiagnosticText()
        };
    }

    /// <summary>
    /// 构建事件批次摘要指纹。
    /// </summary>
    public static string BuildBatchHash(IReadOnlyList<AgentTurnEvent> events)
    {
        var builder = new StringBuilder();
        foreach (var item in events.OrderBy(turnEvent => turnEvent.Sequence))
        {
            builder
                .Append(item.Sequence)
                .Append('|')
                .Append(item.Kind)
                .Append('|')
                .Append(item.ToolCallId?.ToString("D") ?? string.Empty)
                .Append('|')
                .Append(item.ToolName.Trim())
                .Append('|')
                .Append(item.Title.Trim())
                .Append('|')
                .Append(item.Message.Trim())
                .Append('|')
                .Append(item.SuggestedAction)
                .AppendLine();
        }

        return PromptFingerprint.ComputeHash(builder.ToString());
    }

    private static ToolSuggestedAction ResolvePrimaryAction(
        IReadOnlyList<AgentTurnEvent> failedEvents,
        IReadOnlyList<ToolExecutionRecord> toolRecords)
    {
        if (toolRecords.Count > 0)
        {
            return ToolRecoveryStrategySummaryBuilder.Build(toolRecords).PrimaryAction;
        }

        var actions = failedEvents
            .Select(item => item.SuggestedAction)
            .Where(action => action != ToolSuggestedAction.None)
            .ToHashSet();

        return ToolSuggestedActionExtensions.GetRecoveryPriority().FirstOrDefault(actions.Contains);
    }

    private static string? ResolveFirstFailureSummary(IReadOnlyList<AgentTurnEvent> failedEvents)
    {
        var firstFailure = failedEvents.FirstOrDefault();
        if (firstFailure == null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(firstFailure.Message)
            ? firstFailure.Title.Trim()
            : firstFailure.Message.Trim();
    }

    private static string? ResolveFirstFailedToolName(IReadOnlyList<AgentTurnEvent> failedEvents)
    {
        var toolName = failedEvents.FirstOrDefault()?.ToolName.Trim();
        return string.IsNullOrWhiteSpace(toolName) ? null : toolName;
    }

    private static long CalculateTotalDurationMs(IReadOnlyList<ToolExecutionRecord> toolRecords)
    {
        return toolRecords.Sum(record => Math.Max(0L, (long)record.Duration.TotalMilliseconds));
    }

    private static (string? ToolName, long DurationMs) ResolveSlowestTool(
        IReadOnlyList<ToolExecutionRecord> toolRecords)
    {
        return toolRecords
            .Select(record => new
            {
                ToolName = record.ToolName.Trim(),
                DurationMs = Math.Max(0L, (long)record.Duration.TotalMilliseconds)
            })
            .Where(item => item.DurationMs > 0 && !string.IsNullOrWhiteSpace(item.ToolName))
            .OrderByDescending(item => item.DurationMs)
            .ThenBy(item => item.ToolName, StringComparer.Ordinal)
            .Select(item => ((string?)item.ToolName, item.DurationMs))
            .FirstOrDefault();
    }

    private static AgentTurnEvent BuildToolEvent(Guid turnId, ToolExecutionRecord record, int sequence)
    {
        var summary = ToolExecutionEventSummaryBuilder.Build(record);

        return new AgentTurnEvent
        {
            TurnId = turnId,
            Sequence = sequence,
            Kind = record.Success ? AgentTurnEventKind.ToolCompleted : AgentTurnEventKind.ToolFailed,
            ToolCallId = record.ToolCallId,
            ToolName = summary.ToolName,
            Title = summary.Title,
            Message = summary.Message,
            SuggestedAction = summary.SuggestedAction
        };
    }

    private static AgentTurnEventDto ToDto(AgentTurnEvent turnEvent)
    {
        return new AgentTurnEventDto
        {
            TurnId = turnEvent.TurnId,
            Sequence = turnEvent.Sequence,
            Kind = turnEvent.Kind,
            ToolCallId = turnEvent.ToolCallId,
            ToolName = turnEvent.ToolName,
            Title = turnEvent.Title,
            Message = turnEvent.Message,
            SuggestedAction = turnEvent.SuggestedAction
        };
    }
}
