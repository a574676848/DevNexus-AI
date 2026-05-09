namespace DevNexus.Shared.DTOs;

/// <summary>
/// CLI 执行会话终止结果。
/// </summary>
public sealed class CliExecTerminateResultDto
{
    /// <summary>
    /// 会话标识。
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 是否已执行终止。
    /// </summary>
    public bool Terminated { get; set; }

    /// <summary>
    /// 是否在请求前已经结束。
    /// </summary>
    public bool AlreadyExited { get; set; }

    /// <summary>
    /// 结果文案。
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 终止后的会话状态快照。
    /// </summary>
    public CliSessionStateDto? State { get; set; }
}
