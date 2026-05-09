namespace DevNexus.Core.Abstractions;

/// <summary>
/// LLM 提供商工厂接口
/// 从数据库获取配置并创建对应的 Provider 实例
/// </summary>
public interface ILLMProviderFactory
{
    /// <summary>
    /// 获取默认提供商
    /// </summary>
    Task<ILLMProvider> GetDefaultProviderAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 根据数据库 ID 获取提供商
    /// </summary>
    Task<ILLMProvider> GetProviderByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 根据 ProviderId 获取提供商
    /// </summary>
    Task<ILLMProvider> GetProviderByProviderIdAsync(string providerId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取当前激活的提供商配置信息（用于审计）
    /// </summary>
    (string ModelName, string ProviderName, string ProviderId, Guid LLMProviderId, string BaseUrl)? GetCurrentProviderInfo();
    
    /// <summary>
    /// 清除缓存
    /// </summary>
    void InvalidateCache(string? providerId = null);
}
