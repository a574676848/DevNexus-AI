using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Services;

/// <summary>
/// 更新观测摘要服务。
/// </summary>
public interface IUpdateObservabilityService
{
    /// <summary>
    /// 获取控制台摘要。
    /// </summary>
    Task<UpdateObservabilitySummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取控制台详情。
    /// </summary>
    Task<UpdateObservabilityDetailDto> GetDetailsAsync(
        UpdateObservabilityFilterRequest? filter = null,
        CancellationToken cancellationToken = default);
}
