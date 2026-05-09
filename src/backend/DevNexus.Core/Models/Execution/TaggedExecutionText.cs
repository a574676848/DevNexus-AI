namespace DevNexus.Core.Models.Execution;

/// <summary>
/// 统一构造带标签的文本执行结果。
/// </summary>
public static class TaggedExecutionText
{
    /// <summary>
    /// 构造成功结果文本。
    /// </summary>
    public static string Success(string? message = null) => Compose("SUCCESS", message);

    /// <summary>
    /// 构造失败结果文本。
    /// </summary>
    public static string Failure(string? message = null) => Compose("FAILURE", message);

    /// <summary>
    /// 构造异常结果文本。
    /// </summary>
    public static string Exception(string? message = null) => Compose("EXCEPTION", message);

    /// <summary>
    /// 构造信息结果文本。
    /// </summary>
    public static string Info(string? message = null) => Compose("INFO", message);

    /// <summary>
    /// 构造安全拦截结果文本。
    /// </summary>
    public static string SecurityBlocked(string? message = null) => Compose("SECURITY_BLOCKED", message);

    private static string Compose(string tag, string? message)
    {
        return string.IsNullOrWhiteSpace(message)
            ? $"[{tag}]"
            : $"[{tag}] {message}";
    }
}
