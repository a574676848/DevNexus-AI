namespace DevNexus.Core.Models.Execution;

/// <summary>
/// 带标签的执行输出状态。
/// </summary>
public enum TaggedExecutionStatus
{
    None = 0,
    Success = 1,
    Failure = 2,
    Exception = 3,
    Info = 4,
    SecurityBlocked = 5,
    UnknownTagged = 6
}

/// <summary>
/// 统一解析宿主命令、插件与脚本返回的标签文本输出。
/// </summary>
public sealed record TaggedExecutionOutput
{
    /// <summary>
    /// 原始输出。
    /// </summary>
    public string Raw { get; init; } = string.Empty;

    /// <summary>
    /// 去除标签后的正文。
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 解析后的执行状态。
    /// </summary>
    public TaggedExecutionStatus Status { get; init; } = TaggedExecutionStatus.None;

    /// <summary>
    /// 原始标签名。
    /// </summary>
    public string? Tag { get; init; }

    /// <summary>
    /// 是否带有显式标签。
    /// </summary>
    public bool HasExplicitTag => !string.IsNullOrWhiteSpace(Tag);

    /// <summary>
    /// 是否为显式成功标签。
    /// </summary>
    public bool IsExplicitSuccess => Status == TaggedExecutionStatus.Success;

    /// <summary>
    /// 是否为失败类标签。
    /// </summary>
    public bool IsFailureLike =>
        Status is TaggedExecutionStatus.Failure
            or TaggedExecutionStatus.Exception
            or TaggedExecutionStatus.SecurityBlocked;

    /// <summary>
    /// 解析文本输出。
    /// </summary>
    /// <param name="output">原始文本</param>
    /// <returns>结构化解析结果</returns>
    public static TaggedExecutionOutput Parse(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return new TaggedExecutionOutput();
        }

        var normalized = output.Trim();
        if (!normalized.StartsWith("[", StringComparison.Ordinal))
        {
            return new TaggedExecutionOutput
            {
                Raw = output,
                Message = normalized,
                Status = TaggedExecutionStatus.None
            };
        }

        var closingBracketIndex = normalized.IndexOf(']');
        if (closingBracketIndex <= 1)
        {
            return new TaggedExecutionOutput
            {
                Raw = output,
                Message = normalized,
                Status = TaggedExecutionStatus.None
            };
        }

        var tag = normalized[1..closingBracketIndex].Trim();
        var message = normalized[(closingBracketIndex + 1)..].TrimStart();

        return new TaggedExecutionOutput
        {
            Raw = output,
            Message = message,
            Tag = tag,
            Status = ParseStatus(tag)
        };
    }

    private static TaggedExecutionStatus ParseStatus(string tag)
    {
        return tag.Trim().ToUpperInvariant() switch
        {
            "SUCCESS" => TaggedExecutionStatus.Success,
            "FAILURE" => TaggedExecutionStatus.Failure,
            "EXCEPTION" => TaggedExecutionStatus.Exception,
            "INFO" => TaggedExecutionStatus.Info,
            "SECURITY_BLOCKED" => TaggedExecutionStatus.SecurityBlocked,
            _ => TaggedExecutionStatus.UnknownTagged
        };
    }
}
