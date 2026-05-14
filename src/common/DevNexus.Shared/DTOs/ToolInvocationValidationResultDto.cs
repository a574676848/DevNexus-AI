namespace DevNexus.Shared.DTOs;

/// <summary>
/// 工具调用参数验证结果。
/// </summary>
public sealed class ToolInvocationValidationResultDto
{
    /// <summary>
    /// 参数是否有效。
    /// </summary>
    public bool IsValid { get; init; } = true;

    /// <summary>
    /// 失败原因。
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// 面向用户的提示。
    /// </summary>
    public string? UserMessage { get; init; }

    /// <summary>
    /// 是否允许重试。
    /// </summary>
    public bool Retryable { get; init; }
}
