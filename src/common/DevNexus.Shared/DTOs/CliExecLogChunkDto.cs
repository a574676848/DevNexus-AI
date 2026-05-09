namespace DevNexus.Shared.DTOs;

/// <summary>
/// CLI 日志分片 DTO。
/// </summary>
public sealed class CliExecLogChunkDto
{
    /// <summary>
    /// 聊天会话标识。
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 内部会话键。
    /// </summary>
    public string SessionKey { get; set; } = string.Empty;

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

    /// <summary>
    /// 是否命中过归档输出。
    /// </summary>
    public bool HasArchivedOutput { get; set; }

    /// <summary>
    /// 输出观察摘要。
    /// </summary>
    public string? WatchSummary { get; set; }
}
