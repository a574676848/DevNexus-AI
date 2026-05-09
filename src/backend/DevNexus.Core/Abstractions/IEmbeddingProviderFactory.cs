namespace DevNexus.Core.Abstractions;

/// <summary>
/// Embedding Provider 工厂接口
/// 提供 Embedding 服务实例的获取
/// </summary>
public interface IEmbeddingProviderFactory
{
    /// <summary>
    /// 获取默认的 Embedding Provider
    /// </summary>
    IEmbeddingProvider GetDefaultProvider();

    /// <summary>
    /// 根据 Provider ID 获取特定的 Embedding Provider
    /// </summary>
    /// <param name="providerId">数据库中的 Provider ID</param>
    /// <returns>Embedding Provider 实例，如果不存在则返回 null</returns>
    IEmbeddingProvider? GetProvider(Guid providerId);

    /// <summary>
    /// 根据 Provider ID 获取 Embedding Provider，不存在时抛出异常
    /// </summary>
    IEmbeddingProvider GetRequiredProvider(Guid providerId);
}
