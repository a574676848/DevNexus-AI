using DevNexus.Domain.Entities;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 客户端更新事件仓储。
/// </summary>
public interface IUpdateClientEventRepository
{
    /// <summary>
    /// 写入更新事件。
    /// </summary>
    Task<UpdateClientEvent> AddAsync(UpdateClientEvent clientEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定时间后的事件。
    /// </summary>
    Task<IReadOnlyList<UpdateClientEvent>> GetSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken = default);
}
