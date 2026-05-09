using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Services;

/// <summary>
/// 更新 Manifest 决策服务。
/// </summary>
public interface IUpdateManifestService
{
    /// <summary>
    /// 获取客户端更新决策。
    /// </summary>
    Task<UpdateManifestResponse> GetManifestAsync(UpdateManifestRequest request, CancellationToken cancellationToken = default);
}
