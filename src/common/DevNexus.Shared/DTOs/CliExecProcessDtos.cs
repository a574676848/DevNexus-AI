namespace DevNexus.Shared.DTOs;

/// <summary>
/// CLI 执行会话轮询结果。
/// </summary>
public sealed class CliExecPollResultDto
{
    /// <summary>
    /// 会话标识。
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 当前执行会话快照。
    /// </summary>
    public CliSessionStateDto? State { get; set; }

    /// <summary>
    /// 最近输出摘要。
    /// </summary>
    public string OutputTail { get; set; } = string.Empty;

    /// <summary>
    /// 当前输出总长度。
    /// </summary>
    public int OutputLength { get; set; }

    /// <summary>
    /// 是否已退出。
    /// </summary>
    public bool Exited { get; set; }
}

/// <summary>
/// CLI 执行会话日志结果。
/// </summary>
public sealed class CliExecLogResultDto
{
    /// <summary>
    /// 会话标识。
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 原始日志片段。
    /// </summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>
    /// ANSI 剥离后的日志片段。
    /// </summary>
    public string PlainOutput { get; set; } = string.Empty;

    /// <summary>
    /// 当前返回片段的起始索引。
    /// </summary>
    public int StartIndex { get; set; }

    /// <summary>
    /// 当前输出总长度。
    /// </summary>
    public int OutputLength { get; set; }
}
