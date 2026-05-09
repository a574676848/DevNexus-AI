using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Services;

/// <summary>
/// 客户端更新事件服务。
/// </summary>
public interface IUpdateClientEventService
{
    /// <summary>
    /// 上报客户端更新事件。
    /// </summary>
    Task ReportAsync(ReportUpdateClientEventRequest request, CancellationToken cancellationToken = default);
}
