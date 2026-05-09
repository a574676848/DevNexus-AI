using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// 供应商管理 API 服务接口
/// </summary>
public interface IProviderApiService
{
    #region LLM 供应商管理

    /// <summary>
    /// 获取 LLM 供应商列表
    /// </summary>
    Task<List<LLMProviderResponse>> GetLLMProvidersAsync(bool includeDisabled = false);

    /// <summary>
    /// 创建 LLM 供应商
    /// </summary>
    Task<LLMProviderResponse> CreateLLMProviderAsync(CreateLLMProviderRequest request);

    /// <summary>
    /// 更新 LLM 供应商
    /// </summary>
    Task<LLMProviderResponse> UpdateLLMProviderAsync(Guid id, UpdateLLMProviderRequest request);

    /// <summary>
    /// 删除 LLM 供应商
    /// </summary>
    Task DeleteLLMProviderAsync(Guid id);

    /// <summary>
    /// 设置默认 LLM 供应商
    /// </summary>
    Task SetDefaultLLMProviderAsync(Guid providerId);

    /// <summary>
    /// 验证 LLM 供应商连接
    /// </summary>
    Task<ValidateProviderResponse> ValidateLLMProviderAsync(Guid providerId);

    /// <summary>
    /// 测试 LLM 供应商连接（创建前）
    /// </summary>
    Task<ValidateProviderResponse> TestLLMProviderConnectionAsync(CreateLLMProviderRequest request);

    #endregion

    #region Embedding 供应商管理

    /// <summary>
    /// 获取 Embedding 供应商列表
    /// </summary>
    Task<List<EmbeddingProviderResponse>> GetEmbeddingProvidersAsync(bool includeDisabled = false);

    /// <summary>
    /// 获取全局向量维度
    /// </summary>
    Task<int> GetEmbeddingGlobalVectorSizeAsync();

    /// <summary>
    /// 创建 Embedding 供应商
    /// </summary>
    Task<EmbeddingProviderResponse> CreateEmbeddingProviderAsync(CreateEmbeddingProviderRequest request);

    /// <summary>
    /// 更新 Embedding 供应商
    /// </summary>
    Task<EmbeddingProviderResponse> UpdateEmbeddingProviderAsync(Guid id, UpdateEmbeddingProviderRequest request);

    /// <summary>
    /// 删除 Embedding 供应商
    /// </summary>
    Task DeleteEmbeddingProviderAsync(Guid id);

    /// <summary>
    /// 设置默认 Embedding 供应商
    /// </summary>
    Task SetDefaultEmbeddingProviderAsync(Guid providerId);

    /// <summary>
    /// 验证 Embedding 供应商连接
    /// </summary>
    Task<ValidateProviderResponse> ValidateEmbeddingProviderAsync(Guid providerId);

    /// <summary>
    /// 测试 Embedding 供应商连接(创建前)
    /// </summary>
    Task<ValidateProviderResponse> TestEmbeddingProviderConnectionAsync(CreateEmbeddingProviderRequest request);

    #endregion

    #region Search 供应商管理

    /// <summary>
    /// 获取 Search 供应商列表
    /// </summary>
    Task<List<SearchProviderResponse>> GetSearchProvidersAsync(bool includeDisabled = false);

    /// <summary>
    /// 创建 Search 供应商
    /// </summary>
    Task<SearchProviderResponse> CreateSearchProviderAsync(CreateSearchProviderRequest request);

    /// <summary>
    /// 更新 Search 供应商
    /// </summary>
    Task<SearchProviderResponse> UpdateSearchProviderAsync(Guid id, UpdateSearchProviderRequest request);

    /// <summary>
    /// 删除 Search 供应商
    /// </summary>
    Task DeleteSearchProviderAsync(Guid id);

    /// <summary>
    /// 设置默认 Search 供应商
    /// </summary>
    Task<SearchProviderResponse> SetDefaultSearchProviderAsync(Guid providerId);

    /// <summary>
    /// 验证 Search 供应商连接
    /// </summary>
    Task<ValidateProviderResponse> ValidateSearchProviderAsync(Guid providerId);

    /// <summary>
    /// 测试 Search 供应商连接(创建前)
    /// </summary>
    Task<ValidateProviderResponse> TestSearchProviderConnectionAsync(CreateSearchProviderRequest request);

    #endregion

    #region Storage 供应商管理

    /// <summary>
    /// 获取 Storage 供应商列表
    /// </summary>
    Task<List<StorageProviderResponse>> GetStorageProvidersAsync(bool includeDisabled = false);

    /// <summary>
    /// 创建 Storage 供应商
    /// </summary>
    Task<StorageProviderResponse> CreateStorageProviderAsync(CreateStorageProviderRequest request);

    /// <summary>
    /// 更新 Storage 供应商
    /// </summary>
    Task<StorageProviderResponse> UpdateStorageProviderAsync(Guid id, UpdateStorageProviderRequest request);

    /// <summary>
    /// 删除 Storage 供应商
    /// </summary>
    Task DeleteStorageProviderAsync(Guid id);

    /// <summary>
    /// 设置默认 Storage 供应商
    /// </summary>
    Task SetDefaultStorageProviderAsync(Guid providerId);

    /// <summary>
    /// 验证 Storage 供应商连接
    /// </summary>
    Task<ValidateProviderResponse> ValidateStorageProviderAsync(Guid providerId);

    /// <summary>
    /// 测试 Storage 供应商连接(创建前)
    /// </summary>
    Task<ValidateProviderResponse> TestStorageProviderConnectionAsync(CreateStorageProviderRequest request);

    #endregion

    #region 模型定价管理

    /// <summary>
    /// 获取所有模型定价配置
    /// </summary>
    Task<List<ModelPricingResponse>> GetModelPricingsAsync();

    /// <summary>
    /// 创建模型定价配置
    /// </summary>
    Task<ModelPricingResponse> CreateModelPricingAsync(CreateModelPricingRequest request);

    /// <summary>
    /// 更新模型定价配置
    /// </summary>
    Task<ModelPricingResponse> UpdateModelPricingAsync(Guid id, UpdateModelPricingRequest request);

    /// <summary>
    /// 删除模型定价配置
    /// </summary>
    Task DeleteModelPricingAsync(Guid id);

    #endregion
}

