namespace DevNexus.Shared.Constants;

/// <summary>
/// 聊天相关常量
/// </summary>
public static class ChatConstants
{
    /// <summary>
    /// AI 助手发送者 ID (全0 GUID)
    /// </summary>
    public static readonly Guid AssistantSenderId = Guid.Empty;

    /// <summary>
    /// AI 助手发送者名称
    /// </summary>
    public const string AssistantSenderName = "Assistant";

    /// <summary>
    /// AI 助手角色标识
    /// </summary>
    public const string RoleAssistant = "assistant";
    
    /// <summary>
    /// 用户角色标识
    /// </summary>
    public const string RoleUser = "user";

    /// <summary>
    /// 系统角色标识
    /// </summary>
    public const string RoleSystem = "system";

    /// <summary>
    /// 文本消息类型
    /// </summary>
    public const string MessageTypeText = "text";

    /// <summary>
    /// 图片消息类型
    /// </summary>
    public const string MessageTypeImage = "image";

    /// <summary>
    /// 系统消息类型
    /// </summary>
    public const string MessageTypeSystem = "system";

    /// <summary>
    /// 消息状态：等待中
    /// </summary>
    public const string StatusPending = "pending";

    /// <summary>
    /// 消息状态：生成中
    /// </summary>
    public const string StatusInProgress = "in_progress";

    /// <summary>
    /// 消息状态：已完成
    /// </summary>
    public const string StatusCompleted = "completed";

    /// <summary>
    /// 消息状态：错误
    /// </summary>
    public const string StatusError = "error";

    /// <summary>
    /// 消息状态：被截断（max_tokens 限制）
    /// </summary>
    public const string StatusTruncated = "truncated";

    /// <summary>
    /// 消息状态：已取消
    /// </summary>
    public const string StatusCancelled = "cancelled";

    /// <summary>
    /// 规范化发送者类型。
    /// </summary>
    public static string NormalizeSenderType(string? senderType, string fallback = RoleUser)
    {
        var normalized = senderType?.Trim().ToLowerInvariant();

        return normalized switch
        {
            RoleAssistant => RoleAssistant,
            RoleUser => RoleUser,
            RoleSystem => RoleSystem,
            _ => fallback
        };
    }

    /// <summary>
    /// 是否为用户消息。
    /// </summary>
    public static bool IsUserSender(string? senderType)
    {
        return string.Equals(senderType?.Trim(), RoleUser, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 是否为助手消息。
    /// </summary>
    public static bool IsAssistantSender(string? senderType)
    {
        return string.Equals(senderType?.Trim(), RoleAssistant, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 是否为系统消息。
    /// </summary>
    public static bool IsSystemSender(string? senderType)
    {
        return string.Equals(senderType?.Trim(), RoleSystem, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 规范化消息类型。
    /// </summary>
    public static string NormalizeMessageType(string? messageType, string fallback = MessageTypeText)
    {
        var normalized = messageType?.Trim().ToLowerInvariant();

        return normalized switch
        {
            MessageTypeText => MessageTypeText,
            MessageTypeImage => MessageTypeImage,
            MessageTypeSystem => MessageTypeSystem,
            _ => fallback
        };
    }

    /// <summary>
    /// 是否为文本消息。
    /// </summary>
    public static bool IsTextMessageType(string? messageType)
    {
        return string.Equals(messageType?.Trim(), MessageTypeText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 是否为图片消息。
    /// </summary>
    public static bool IsImageMessageType(string? messageType)
    {
        return string.Equals(messageType?.Trim(), MessageTypeImage, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 规范化消息状态。
    /// </summary>
    public static string NormalizeStatus(string? status, string fallback = StatusCompleted)
    {
        var normalized = status?.Trim().ToLowerInvariant();

        return normalized switch
        {
            StatusPending => StatusPending,
            StatusInProgress => StatusInProgress,
            StatusCompleted => StatusCompleted,
            StatusError => StatusError,
            StatusTruncated => StatusTruncated,
            StatusCancelled => StatusCancelled,
            _ => fallback
        };
    }

    /// <summary>
    /// 是否处于生成中状态。
    /// </summary>
    public static bool IsInProgressStatus(string? status)
    {
        return string.Equals(status?.Trim(), StatusInProgress, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 是否为完成态。
    /// </summary>
    public static bool IsCompletedStatus(string? status)
    {
        return string.Equals(status?.Trim(), StatusCompleted, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 是否为错误态。
    /// </summary>
    public static bool IsErrorStatus(string? status)
    {
        return string.Equals(status?.Trim(), StatusError, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 是否为终止态。
    /// </summary>
    public static bool IsTerminalStatus(string? status)
    {
        var normalized = NormalizeStatus(status, string.Empty);
        return normalized == StatusCompleted
            || normalized == StatusError
            || normalized == StatusCancelled
            || normalized == StatusTruncated;
    }
}
