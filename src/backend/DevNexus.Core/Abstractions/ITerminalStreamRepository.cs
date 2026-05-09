using DevNexus.Domain.Entities;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// 终端流仓储接口
/// </summary>
public interface ITerminalStreamRepository
{
    /// <summary>
    /// 根据 ID 获取终端流
    /// </summary>
    Task<TerminalStream?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据消息 ID 获取所有终端流
    /// </summary>
    Task<List<TerminalStream>> GetByMessageIdAsync(Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据一组消息 ID 批量获取所有终端流。
    /// </summary>
    Task<List<TerminalStream>> GetByMessageIdsAsync(
        IReadOnlyCollection<Guid> messageIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定会话当前仍活跃的终端流。
    /// </summary>
    Task<List<TerminalStream>> GetActiveBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据工具调用 ID 获取终端流
    /// </summary>
    Task<TerminalStream?> GetByToolCallIdAsync(Guid toolCallId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建终端流
    /// </summary>
    Task<TerminalStream> CreateAsync(TerminalStream stream, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新终端流
    /// </summary>
    Task UpdateAsync(TerminalStream stream, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量创建终端流
    /// </summary>
    Task<List<TerminalStream>> CreateBatchAsync(List<TerminalStream> streams, CancellationToken cancellationToken = default);
}
