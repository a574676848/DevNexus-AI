using DevNexus.Domain.Entities;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 用户外部系统集成管理服务接口
/// </summary>
public interface IUserIntegrationService
{
    /// <summary>
    /// 获取用户的所有集成
    /// </summary>
    Task<IEnumerable<UserIntegration>> GetUserIntegrationsAsync(Guid userId, IntegrationType? type = null, bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有用户的集成（管理员专用）
    /// </summary>
    Task<IEnumerable<UserIntegration>> GetAllIntegrationsAsync(IntegrationType? type = null, bool includeInactive = false, Guid? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有用户的集成详情（管理员专用）。
    /// </summary>
    Task<IEnumerable<UserIntegrationDetailedResponse>> GetAllIntegrationDetailsAsync(
        IntegrationType? type = null,
        bool includeInactive = false,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户集成详情
    /// </summary>
    Task<UserIntegration?> GetUserIntegrationByIdAsync(Guid userId, Guid integrationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户的默认集成
    /// </summary>
    Task<UserIntegration?> GetDefaultIntegrationAsync(Guid userId, IntegrationType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建用户集成
    /// </summary>
    /// <param name="userId">目标用户ID</param>
    /// <param name="request">创建请求</param>
    /// <param name="isAdminCreate">是否为管理员创建</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<UserIntegration> CreateIntegrationAsync(Guid userId, CreateUserIntegrationRequest request, bool isAdminCreate = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新用户集成
    /// </summary>
    Task<UserIntegration> UpdateIntegrationAsync(Guid userId, Guid integrationId, UpdateUserIntegrationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除用户集成
    /// </summary>
    Task<bool> DeleteIntegrationAsync(Guid userId, Guid integrationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 设置为默认集成
    /// </summary>
    Task<bool> SetAsDefaultAsync(Guid userId, Guid integrationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证集成连接
    /// </summary>
    Task<ValidateUserIntegrationResponse> ValidateIntegrationAsync(Guid userId, Guid integrationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 测试集成连接（创建前测试）
    /// </summary>
    Task<ValidateUserIntegrationResponse> TestIntegrationAsync(ValidateUserIntegrationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 记录集成使用
    /// </summary>
    Task RecordUsageAsync(Guid userId, Guid integrationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取用户集成统计
    /// </summary>
    Task<UserIntegrationStatsResponse> GetUserIntegrationStatsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取解密后的凭证（仅内部服务使用）
    /// </summary>
    Task<string> GetDecryptedCredentialAsync(Guid userId, Guid integrationId, CancellationToken cancellationToken = default);
}
