using DevNexus.Shared.DTOs;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 模型定价管理服务接口
/// </summary>
public interface IModelPricingService
{
    /// <summary>
    /// 获取所有定价配置
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>定价配置列表</returns>
    Task<IEnumerable<ModelPricingResponse>> GetAllPricingsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据定价 ID 获取定价配置
    /// </summary>
    /// <param name="id">定价配置 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>定价配置</returns>
    Task<ModelPricingResponse?> GetPricingByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据 LLM Provider ID 获取定价配置（支持 Redis 缓存）
    /// </summary>
    /// <param name="providerId">LLM Provider ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>定价配置</returns>
    Task<ModelPricingResponse?> GetPricingByProviderIdAsync(
        Guid providerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据提供商类型和提供商 ID 获取定价配置。
    /// </summary>
    Task<ModelPricingResponse?> GetPricingByProviderAsync(
        string providerType,
        Guid providerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据 LLM Provider 的 ProviderId 字符串获取定价配置（支持 Redis 缓存）
    /// </summary>
    /// <param name="providerProviderId">LLM Provider 的 ProviderId 字符串</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>定价配置</returns>
    Task<ModelPricingResponse?> GetPricingByProviderProviderIdAsync(
        string providerProviderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据提供商类型与提供商标识获取定价配置。
    /// </summary>
    Task<ModelPricingResponse?> GetPricingByProviderProviderIdAsync(
        string providerType,
        string providerProviderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建定价配置
    /// </summary>
    /// <param name="request">创建请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建的定价配置</returns>
    Task<ModelPricingResponse> CreatePricingAsync(
        CreateModelPricingRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新定价配置
    /// </summary>
    /// <param name="id">定价配置 ID</param>
    /// <param name="request">更新请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新后的定价配置</returns>
    Task<ModelPricingResponse> UpdatePricingAsync(
        Guid id,
        UpdateModelPricingRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除定价配置
    /// </summary>
    /// <param name="id">定价配置 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否删除成功</returns>
    Task<bool> DeletePricingAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
