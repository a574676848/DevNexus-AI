using DevNexus.Shared.DTOs;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// LLM供应商管理服务接口
/// </summary>
public interface ILLMProviderManagementService
{
    /// <summary>
    /// 获取所有供应商
    /// </summary>
    Task<IEnumerable<LLMProviderResponse>> GetAllProvidersAsync(
        bool includeDisabled = false,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 根据ID获取供应商
    /// </summary>
    Task<LLMProviderResponse?> GetProviderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 根据ProviderId获取供应商
    /// </summary>
    Task<LLMProviderResponse?> GetProviderByProviderIdAsync(
        string providerId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取默认供应商
    /// </summary>
    Task<LLMProviderResponse?> GetDefaultProviderAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 创建供应商
    /// </summary>
    Task<LLMProviderResponse> CreateProviderAsync(
        CreateLLMProviderRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 更新供应商
    /// </summary>
    Task<LLMProviderResponse> UpdateProviderAsync(
        Guid id,
        UpdateLLMProviderRequest request,
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
        CreateLLMProviderRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取解密的 API Key（仅供工厂内部使用）
    /// </summary>
    Task<string> GetDecryptedApiKeyAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
