namespace DevNexus.Shared.DTOs;

/// <summary>
/// CLI 执行会话 DTO。
/// </summary>
public sealed class CliExecSessionDto
{
    /// <summary>
    /// 聊天会话标识。
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 统一会话状态。
    /// </summary>
    public CliSessionStateDto? State { get; set; }

    /// <summary>
    /// 最近输出尾部。
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

    /// <summary>
    /// 最近一次可用快照。
    /// </summary>
    public CliExecCheckpointDto? LatestCheckpoint { get; set; }
}
