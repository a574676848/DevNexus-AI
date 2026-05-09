using DevNexus.Domain.Entities;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// CLI 执行快照仓储接口。
/// </summary>
public interface ICliExecCheckpointRepository
{
    /// <summary>
    /// 获取指定会话所有有效快照。
    /// </summary>
    Task<IReadOnlyList<CliExecCheckpoint>> GetActiveBySessionKeyAsync(
        string sessionKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定会话最近的有效快照。
    /// </summary>
    Task<CliExecCheckpoint?> GetLatestActiveBySessionKeyAsync(string sessionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增快照。
    /// </summary>
    Task AddAsync(CliExecCheckpoint checkpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新快照。
    /// </summary>
    Task UpdateAsync(CliExecCheckpoint checkpoint, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量更新快照。
    /// </summary>
    Task UpdateRangeAsync(IEnumerable<CliExecCheckpoint> checkpoints, CancellationToken cancellationToken = default);
}
