using DevNexus.Shared.Enums;

namespace DevNexus.Shared.Constants;

/// <summary>
/// 工具结果块与交互卡片 metadata 的共享协议定义。
/// </summary>
public static class ToolBlockMetadataConstants
{
    public const string ToolName = "toolName";
    public const string ToolCallId = "toolCallId";
    public const string CardType = "cardType";
    public const string Title = "title";
    public const string Status = "status";
    public const string ActionText = "actionText";
    public const string ActionUrl = "actionUrl";
    public const string Query = "query";
    public const string Url = "url";
    public const string Method = "method";
    public const string Duration = "duration";
    public const string ActionId = "actionId";
    public const string MaxResults = "maxResults";
    public const string Message = "message";
    public const string IsProcessed = "isProcessed";
    public const string IsApproved = "isApproved";
    public const string FileTaskId = "fileTaskId";
    public const string TaskType = "taskType";
    public const string Stage = "stage";
    public const string StageSummary = "stageSummary";
    public const string InputCount = "inputCount";
    public const string OutputCount = "outputCount";
    public const string IntentConfidence = "intentConfidence";
    public const string IntentReason = "intentReason";
    public const string IntentDecisionSource = "intentDecisionSource";
    public const string ToolNameUnknown = "unknown";
    public const string ToolNameFallbackDisplay = "工具";

    public const string CardTypeUnknown = "unknown";
    public const string CardTypeSearch = "search";
    public const string CardTypeAdvancedSearch = "advanced-search";
    public const string CardTypeWebpage = "webpage";
    public const string CardTypeFileTask = "file-task";
    public const string CardTypeCommand = "command";
    public const string CardTypeSql = "sql";
    public const string CardTypeScript = "script";
    public const string CardTypeFile = "file";

    public const string StatusLoading = "loading";
    public const string StatusSuccess = "success";
    public const string StatusError = "error";

    /// <summary>
    /// 规范化交互卡片类型。
    /// </summary>
    public static string NormalizeCardType(string? cardType, string fallback = CardTypeUnknown)
    {
        return cardType?.Trim().ToLowerInvariant() switch
        {
            CardTypeSearch => CardTypeSearch,
            CardTypeAdvancedSearch => CardTypeAdvancedSearch,
            CardTypeWebpage => CardTypeWebpage,
            CardTypeFileTask => CardTypeFileTask,
            CardTypeCommand => CardTypeCommand,
            CardTypeSql => CardTypeSql,
            CardTypeScript => CardTypeScript,
            CardTypeFile => CardTypeFile,
            CardTypeUnknown => CardTypeUnknown,
            _ => fallback
        };
    }

    /// <summary>
    /// 判断是否为搜索类交互卡片。
    /// </summary>
    public static bool IsSearchLikeCardType(string? cardType)
    {
        var normalized = NormalizeCardType(cardType, string.Empty);
        return normalized is CardTypeSearch or CardTypeAdvancedSearch or CardTypeWebpage;
    }

    /// <summary>
    /// 规范化工具块状态。
    /// </summary>
    public static string NormalizeStatus(string? status, string fallback = StatusSuccess)
    {
        var normalized = status?.Trim().ToLowerInvariant();
        if (normalized == StatusLoading)
        {
            return StatusLoading;
        }

        return ToolInvocationStatusExtensions.Parse(normalized) switch
        {
            ToolInvocationStatus.Queued => StatusLoading,
            ToolInvocationStatus.Pending => StatusLoading,
            ToolInvocationStatus.Running => StatusLoading,
            ToolInvocationStatus.Completed => StatusSuccess,
            ToolInvocationStatus.Failed => StatusError,
            ToolInvocationStatus.Cancelled => StatusError,
            ToolInvocationStatus.Timeout => StatusError,
            _ => fallback
        };
    }

    /// <summary>
    /// 是否为加载中状态。
    /// </summary>
    public static bool IsLoadingStatus(string? status)
    {
        return NormalizeStatus(status, string.Empty) == StatusLoading;
    }

    /// <summary>
    /// 是否为错误状态。
    /// </summary>
    public static bool IsErrorStatus(string? status)
    {
        return NormalizeStatus(status, string.Empty) == StatusError;
    }

}
