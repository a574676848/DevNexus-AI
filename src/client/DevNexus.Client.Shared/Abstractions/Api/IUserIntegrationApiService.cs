using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// 用户外部系统集成 API 服务接口
/// </summary>
public interface IUserIntegrationApiService
{
    /// <summary>
    /// 获取当前用户的所有集成
    /// </summary>
    Task<IEnumerable<UserIntegrationResponse>> GetUserIntegrationsAsync(
        IntegrationType? type = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有用户的集成（管理员专用）
    /// </summary>
    Task<IEnumerable<UserIntegrationDetailedResponse>> GetAllIntegrationsAsync(
        IntegrationType? type = null,
        bool includeInactive = false,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取集成详情
    /// </summary>
    Task<UserIntegrationResponse?> GetIntegrationByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建新的集成
    /// </summary>
    Task<UserIntegrationResponse> CreateIntegrationAsync(
        CreateUserIntegrationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新集成
    /// </summary>
    Task<UserIntegrationResponse> UpdateIntegrationAsync(
        Guid id,
        UpdateUserIntegrationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除集成
    /// </summary>
    Task<bool> DeleteIntegrationAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置为默认集成
    /// </summary>
    Task<bool> SetAsDefaultIntegrationAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证集成连接
    /// </summary>
    Task<ValidateUserIntegrationResponse> ValidateIntegrationAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 测试集成连接（创建前测试）
    /// </summary>
    Task<ValidateUserIntegrationResponse> TestIntegrationConnectionAsync(
        ValidateUserIntegrationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户集成统计
    /// </summary>
    Task<UserIntegrationStatsResponse> GetIntegrationStatsAsync(
        CancellationToken cancellationToken = default);
}

