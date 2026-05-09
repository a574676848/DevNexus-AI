using DevNexus.Shared.DTOs;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 搜索供应商管理服务接口
/// </summary>
public interface ISearchProviderManagementService
{
    /// <summary>
    /// 获取所有搜索供应商
    /// </summary>
    Task<IEnumerable<SearchProviderResponse>> GetAllProvidersAsync(
        bool includeDisabled = false,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 根据ID获取搜索供应商
    /// </summary>
    Task<SearchProviderResponse?> GetProviderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 根据ProviderId获取搜索供应商
    /// </summary>
    Task<SearchProviderResponse?> GetProviderByProviderIdAsync(
        string providerId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取默认搜索供应商
    /// </summary>
    Task<SearchProviderResponse?> GetDefaultProviderAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 创建搜索供应商
    /// </summary>
    Task<SearchProviderResponse> CreateProviderAsync(
        CreateSearchProviderRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 更新搜索供应商
    /// </summary>
    Task<SearchProviderResponse> UpdateProviderAsync(
        Guid id,
        UpdateSearchProviderRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 删除搜索供应商
    /// </summary>
    Task<bool> DeleteProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 设置默认搜索供应商
    /// </summary>
    Task<SearchProviderResponse> SetDefaultProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 验证搜索供应商配置
    /// </summary>
    Task<ValidateProviderResponse> ValidateProviderAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取解密后的 API Key（仅供内部服务使用）
    /// </summary>
    Task<string> GetDecryptedApiKeyAsync(Guid providerId);

    /// <summary>
    /// 测试搜索供应商连接（创建前测试）
    /// </summary>
    Task<ValidateProviderResponse> TestProviderConnectionAsync(
        CreateSearchProviderRequest request,
        CancellationToken cancellationToken = default);
}
