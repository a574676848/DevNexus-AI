namespace DevNexus.Shared.DTOs;

/// <summary>
/// 终端完整输出内容 DTO。
/// </summary>
public sealed class TerminalOutputContentDto
{
    /// <summary>
    /// 终端记录标识。
    /// </summary>
    public Guid RecordId { get; set; }

    /// <summary>
    /// 输出内容。
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 是否来自归档输出。
    /// </summary>
    public bool HasArchivedOutput { get; set; }

    /// <summary>
    /// 输出字符数。
    /// </summary>
    public int OutputLength { get; set; }

    /// <summary>
    /// 输出行数。
    /// </summary>
    public int OutputLineCount { get; set; }

    /// <summary>
    /// 输出观察摘要。
    /// </summary>
    public string? WatchSummary { get; set; }
}
