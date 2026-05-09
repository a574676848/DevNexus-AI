using DevNexus.Shared.DTOs;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 存储供应商管理服务接口
/// </summary>
public interface IStorageProviderManagementService
{
    /// <summary>
    /// 获取所有供应商
    /// </summary>
    Task<IEnumerable<StorageProviderResponse>> GetAllProvidersAsync(
        bool includeDisabled = false,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 根据ID获取供应商
    /// </summary>
    Task<StorageProviderResponse?> GetProviderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 根据ProviderId获取供应商
    /// </summary>
    Task<StorageProviderResponse?> GetProviderByProviderIdAsync(
        string providerId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取默认供应商
    /// </summary>
    Task<StorageProviderResponse?> GetDefaultProviderAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 创建供应商
    /// </summary>
    Task<StorageProviderResponse> CreateProviderAsync(
        CreateStorageProviderRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 更新供应商
    /// </summary>
    Task<StorageProviderResponse> UpdateProviderAsync(
        Guid id,
        UpdateStorageProviderRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 删除供应商
    /// </summary>
    Task<bool> DeleteProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 设置默认供应商
    /// </summary>
    Task<bool> SetDefaultProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 验证供应商配置
    /// </summary>
    Task<ValidateProviderResponse> ValidateProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 测试供应商连接
    /// </summary>
    Task<ValidateProviderResponse> TestProviderConnectionAsync(
        CreateStorageProviderRequest request,
        CancellationToken cancellationToken = default);
}
