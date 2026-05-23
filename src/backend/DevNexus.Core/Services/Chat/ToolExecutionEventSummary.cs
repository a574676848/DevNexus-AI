using DevNexus.Core.Models.Evaluation;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 工具执行事件摘要。
/// </summary>
internal sealed record ToolExecutionEventSummary
{
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
/// 工具执行事件摘要构建器。
/// </summary>
internal static class ToolExecutionEventSummaryBuilder
{
    private const int MaxMessageLength = 180;

    /// <summary>
    /// 为单条工具记录构建事件摘要。
    /// </summary>
    public static ToolExecutionEventSummary Build(ToolExecutionRecord record)
    {
        var title = record.Success ? "工具执行完成" : ResolveFailureTitle(record);
        var message = record.Success
            ? ResolveSuccessMessage(record)
            : ResolveFailureMessage(record);

        return new ToolExecutionEventSummary
        {
            ToolName = record.ToolName,
            Title = title,
            Message = ToolOutputBudgetCompressor.Compress(message, MaxMessageLength),
            SuggestedAction = record.SuggestedAction
        };
    }

    /// <summary>
    /// 构建多条失败记录的合并摘要。
    /// </summary>
    public static string BuildFailureDigest(IReadOnlyList<ToolExecutionRecord> records, int maxItems = 2)
    {
        var messages = records
            .Where(record => !record.Success)
            .Select(record => FormatFailureDigestItem(record, Build(record)))
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.Ordinal)
            .Take(maxItems)
            .ToList();

        return messages.Count == 0 ? string.Empty : string.Join("；", messages);
    }

    private static string FormatFailureDigestItem(ToolExecutionRecord record, ToolExecutionEventSummary summary)
    {
        var toolName = string.IsNullOrWhiteSpace(record.ToolName)
            ? "UnknownTool"
            : record.ToolName;

        return $"{toolName}: failureReason={record.FailureReason.ToWireValue()}, " +
               $"suggestedAction={summary.SuggestedAction.ToWireValue()}, message={summary.Message}";
    }

    private static string ResolveFailureTitle(ToolExecutionRecord record)
    {
        return record.SuggestedAction switch
        {
            ToolSuggestedAction.RequestApproval => "工具等待审批",
            ToolSuggestedAction.RefreshCredential => "工具需要刷新凭证",
            ToolSuggestedAction.PromptUserInput => "工具需要补充输入",
            ToolSuggestedAction.StopCommand => "工具需要停止命令",
            ToolSuggestedAction.Retry => "工具建议重试",
            ToolSuggestedAction.Abort => "工具执行已终止",
            _ => "工具执行失败"
        };
    }

    private static string ResolveSuccessMessage(ToolExecutionRecord record)
    {
        return !string.IsNullOrWhiteSpace(record.Output)
            ? record.Output!
            : $"{record.ToolName} 已完成。";
    }

    private static string ResolveFailureMessage(ToolExecutionRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.UserMessage))
        {
            return record.UserMessage!;
        }

        if (!string.IsNullOrWhiteSpace(record.ErrorSummary))
        {
            return record.ErrorSummary!;
        }

        if (!string.IsNullOrWhiteSpace(record.ErrorMessage))
        {
            return record.ErrorMessage!;
        }

        return record.FailureReason switch
        {
            ToolFailureReason.ApprovalRequired => "当前操作需要审批后才能继续执行。",
            ToolFailureReason.AuthExpired or ToolFailureReason.AuthPermanent => "当前操作需要刷新凭证后才能继续执行。",
            ToolFailureReason.MissingUserInput => "当前操作需要补充输入后才能继续执行。",
            ToolFailureReason.ContextOverflow => "当前工具输出或上下文过长，需要压缩后继续。",
            ToolFailureReason.ToolFormatError => "工具参数或返回格式异常，需要修正后继续。",
            _ => "工具执行失败，但没有返回可用的错误摘要。"
        };
    }

}
