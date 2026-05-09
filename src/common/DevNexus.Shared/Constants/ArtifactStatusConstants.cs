namespace DevNexus.Shared.Constants;

/// <summary>
/// Artifact 解析状态字符串协议定义与判断入口。
/// </summary>
public static class ArtifactStatusConstants
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Parsed = "Parsed";
    public const string Indexing = "Indexing";
    public const string Completed = "Completed";
    public const string FailedPrefix = "Failed";

    /// <summary>
    /// 规范化 Artifact 状态字符串。
    /// </summary>
    public static string Normalize(string? status, string fallback = Processing)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return fallback;
        }

        var value = status.Trim();

        if (value.Equals(Pending, StringComparison.OrdinalIgnoreCase))
        {
            return Pending;
        }

        if (value.Equals(Processing, StringComparison.OrdinalIgnoreCase))
        {
            return Processing;
        }

        if (value.Equals(Parsed, StringComparison.OrdinalIgnoreCase))
        {
            return Parsed;
        }

        if (value.Equals(Indexing, StringComparison.OrdinalIgnoreCase))
        {
            return Indexing;
        }

        if (value.Equals(Completed, StringComparison.OrdinalIgnoreCase))
        {
            return Completed;
        }

        if (IsFailed(value))
        {
            return BuildFailedStatus(ExtractFailureMessage(value));
        }

        return value;
    }

    /// <summary>
    /// 是否为已完成状态。
    /// </summary>
    public static bool IsCompleted(string? status)
    {
        return Normalize(status, string.Empty).Equals(Completed, StringComparison.Ordinal);
    }

    /// <summary>
    /// 是否为失败状态。
    /// </summary>
    public static bool IsFailed(string? status)
    {
        return !string.IsNullOrWhiteSpace(status)
            && status.Trim().StartsWith(FailedPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 构造失败状态协议值。
    /// </summary>
    public static string BuildFailedStatus(string? message)
    {
        return string.IsNullOrWhiteSpace(message)
            ? FailedPrefix
            : $"{FailedPrefix}: {message.Trim()}";
    }

    /// <summary>
    /// 从失败状态协议值中提取错误消息。
    /// </summary>
    public static string? ExtractFailureMessage(string? status)
    {
        if (!IsFailed(status))
        {
            return null;
        }

        var normalized = status!.Trim();
        if (normalized.Length == FailedPrefix.Length)
        {
            return null;
        }

        var detail = normalized[FailedPrefix.Length..].TrimStart(':', ' ');
        return string.IsNullOrWhiteSpace(detail) ? null : detail;
    }
}
