namespace DevNexus.Shared.DTOs;

/// <summary>
/// CLI 执行回滚结果。
/// </summary>
public sealed class CliExecRollbackResultDto
{
    /// <summary>
    /// 会话标识。
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// 是否已成功回滚。
    /// </summary>
    public bool RolledBack { get; set; }

    /// <summary>
    /// 提示文案。
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 工作目录。
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// 回滚后的会话状态快照。
    /// </summary>
    public CliSessionStateDto? State { get; set; }
}
