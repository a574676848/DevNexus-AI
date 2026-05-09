using DevNexus.Shared.DTOs;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// Embedding供应商管理服务接口
/// </summary>
public interface IEmbeddingProviderManagementService
{
    /// <summary>
    /// 获取全局统一的向量维度。
    /// </summary>
    int GetGlobalVectorSize();

    /// <summary>
    /// 获取所有供应商
    /// </summary>
    Task<IEnumerable<EmbeddingProviderResponse>> GetAllProvidersAsync(
        bool includeDisabled = false,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 根据ID获取供应商
    /// </summary>
    Task<EmbeddingProviderResponse?> GetProviderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 根据ProviderId获取供应商
    /// </summary>
    Task<EmbeddingProviderResponse?> GetProviderByProviderIdAsync(
        string providerId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取默认供应商
    /// </summary>
    Task<EmbeddingProviderResponse?> GetDefaultProviderAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 创建供应商
    /// </summary>
    Task<EmbeddingProviderResponse> CreateProviderAsync(
        CreateEmbeddingProviderRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 更新供应商
    /// </summary>
    Task<EmbeddingProviderResponse> UpdateProviderAsync(
        Guid id,
        UpdateEmbeddingProviderRequest request,
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
        CreateEmbeddingProviderRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取供应商凭据（包含解密后的 API Key）
    /// 仅供内部 Factory 使用
    /// </summary>
    Task<ProviderCredentials?> GetProviderCredentialsAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 供应商凭据（内部使用）
/// </summary>
public record ProviderCredentials(
    string Endpoint,
    string ApiKey,
    string ModelName,
    int VectorSize,
    string ProviderType);
