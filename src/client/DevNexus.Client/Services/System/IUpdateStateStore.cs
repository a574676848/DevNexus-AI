using DevNexus.Client.Shared.Abstractions;

namespace DevNexus.Client.Services.System;

/// <summary>
/// 更新执行状态存储。
/// </summary>
public interface IUpdateStateStore
{
    /// <summary>
    /// 读取最近一次更新执行快照。
    /// </summary>
    Task<UpdateExecutionSnapshot?> GetAsync();

    /// <summary>
    /// 保存更新执行快照。
    /// </summary>
    Task SaveAsync(UpdateExecutionSnapshot snapshot);

    /// <summary>
    /// 清空更新执行快照。
    /// </summary>
    Task ClearAsync();
}
