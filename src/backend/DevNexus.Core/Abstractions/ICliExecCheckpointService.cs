using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// CLI 执行快照服务。
/// </summary>
public interface ICliExecCheckpointService
{
    /// <summary>
    /// 对高风险命令创建执行前快照。
    /// </summary>
    Task CreateCheckpointIfNeededAsync(
        Guid userId,
        Guid? chatSessionId,
        string sessionKey,
        string command,
        string workingDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 回滚指定会话最近一次有效快照。
    /// </summary>
    Task<CliExecRollbackResultDto> RollbackLatestAsync(
        Guid userId,
        Guid sessionId,
        string sessionKey,
        CancellationToken cancellationToken = default);
}
