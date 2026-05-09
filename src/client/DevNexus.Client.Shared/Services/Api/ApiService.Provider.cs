using DevNexus.Client.Shared.Services.Exceptions;
using DevNexus.Shared.DTOs;
using System.Net.Http.Json;
namespace DevNexus.Client.Shared.Services.Api;

/// <summary>
/// REST API 服务 - 供应商管理部分 (LLM, Embedding, Search, Storage)
/// </summary>
public partial class ApiService
{
    #region LLM 供应商管理

    /// <inheritdoc />
    public async Task<List<LLMProviderResponse>> GetLLMProvidersAsync(bool includeDisabled = false)
    {
        var response = await _httpClient.GetAsync($"/api/v1/providers/llm?includeDisabled={includeDisabled}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<LLMProviderResponse>>() ?? new();
    }

    /// <inheritdoc />
    public async Task<LLMProviderResponse> CreateLLMProviderAsync(CreateLLMProviderRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/providers/llm", request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<LLMProviderResponse>()
            ?? throw new ApiException("创建 LLM 供应商失败");
    }

    /// <inheritdoc />
    public async Task<LLMProviderResponse> UpdateLLMProviderAsync(Guid id, UpdateLLMProviderRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/providers/llm/{id}", request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<LLMProviderResponse>()
            ?? throw new ApiException("更新 LLM 供应商失败");
    }

    /// <inheritdoc />
    public async Task DeleteLLMProviderAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"/api/v1/providers/llm/{id}");
        await EnsureSuccessAsync(response);
    }

    /// <inheritdoc />
    public async Task SetDefaultLLMProviderAsync(Guid providerId)
    {
        var response = await _httpClient.PostAsync($"/api/v1/providers/llm/{providerId}/set-default", null);
        await EnsureSuccessAsync(response);
    }

    /// <inheritdoc />
    public async Task<ValidateProviderResponse> ValidateLLMProviderAsync(Guid providerId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/v1/providers/llm/{providerId}/validate", null);
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<ValidateProviderResponse>()
                ?? new ValidateProviderResponse { IsValid = false, ErrorMessage = "验证失败" };
        }
        catch (Exception ex)
        {
            return new ValidateProviderResponse { IsValid = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public async Task<ValidateProviderResponse> TestLLMProviderConnectionAsync(CreateLLMProviderRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/v1/providers/llm/test", request);
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<ValidateProviderResponse>()
                ?? new ValidateProviderResponse { IsValid = false, ErrorMessage = "测试失败" };
        }
        catch (Exception ex)
        {
            return new ValidateProviderResponse { IsValid = false, ErrorMessage = ex.Message };
        }
    }

    #endregion

    #region Embedding 供应商管理

    /// <inheritdoc />
    public async Task<List<EmbeddingProviderResponse>> GetEmbeddingProvidersAsync(bool includeDisabled = false)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/v1/providers/embedding?includeDisabled={includeDisabled}");
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<List<EmbeddingProviderResponse>>() ?? new();
        }
        catch
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> GetEmbeddingGlobalVectorSizeAsync()
    {
        var response = await _httpClient.GetAsync("/api/v1/providers/embedding/global-vector-size");
        await EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<EmbeddingGlobalVectorSizeResponse>();
        return result?.VectorSize ?? 1024;
    }

    /// <inheritdoc />
    public async Task<EmbeddingProviderResponse> CreateEmbeddingProviderAsync(CreateEmbeddingProviderRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/v1/providers/embedding", request);
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<EmbeddingProviderResponse>()
                ?? throw new ApiException("创建 Embedding 供应商失败");
        }
        catch
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<EmbeddingProviderResponse> UpdateEmbeddingProviderAsync(Guid id, UpdateEmbeddingProviderRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/v1/providers/embedding/{id}", request);
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<EmbeddingProviderResponse>()
                ?? throw new ApiException("更新 Embedding 供应商失败");
        }
        catch
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteEmbeddingProviderAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/v1/providers/embedding/{id}");
            await EnsureSuccessAsync(response);
        }
        catch
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SetDefaultEmbeddingProviderAsync(Guid providerId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/v1/providers/embedding/{providerId}/set-default", null);
            await EnsureSuccessAsync(response);
        }
        catch
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ValidateProviderResponse> ValidateEmbeddingProviderAsync(Guid providerId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/v1/providers/embedding/{providerId}/validate", null);
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<ValidateProviderResponse>()
                ?? new ValidateProviderResponse { IsValid = false, ErrorMessage = "验证失败" };
        }
        catch (Exception ex)
        {
            return new ValidateProviderResponse { IsValid = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public async Task<ValidateProviderResponse> TestEmbeddingProviderConnectionAsync(CreateEmbeddingProviderRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/v1/providers/embedding/test", request);
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<ValidateProviderResponse>()
                ?? new ValidateProviderResponse { IsValid = false, ErrorMessage = "测试失败" };
        }
        catch (Exception ex)
        {
            return new ValidateProviderResponse { IsValid = false, ErrorMessage = ex.Message };
        }
    }

    #endregion

    private sealed class EmbeddingGlobalVectorSizeResponse
    {
        public int VectorSize { get; set; }
    }

    #region Search 供应商管理

    /// <inheritdoc />
    public async Task<List<SearchProviderResponse>> GetSearchProvidersAsync(bool includeDisabled = false)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/v1/providers/search?includeDisabled={includeDisabled}");
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<List<SearchProviderResponse>>() ?? new();
        }
        catch
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<SearchProviderResponse> CreateSearchProviderAsync(CreateSearchProviderRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/v1/providers/search", request);
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<SearchProviderResponse>()
                ?? throw new ApiException("创建 Search 供应商失败");
        }
        catch
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<SearchProviderResponse> UpdateSearchProviderAsync(Guid id, UpdateSearchProviderRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/v1/providers/search/{id}", request);
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<SearchProviderResponse>()
                ?? throw new ApiException("更新 Search 供应商失败");
        }
        catch
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteSearchProviderAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/v1/providers/search/{id}");
            await EnsureSuccessAsync(response);
        }
        catch
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<SearchProviderResponse> SetDefaultSearchProviderAsync(Guid providerId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/v1/providers/search/{providerId}/set-default", null);
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<SearchProviderResponse>()
                ?? throw new ApiException("设置默认 Search 供应商失败");
        }
        catch
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ValidateProviderResponse> ValidateSearchProviderAsync(Guid providerId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/v1/providers/search/{providerId}/validate", null);
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<ValidateProviderResponse>()
                ?? new ValidateProviderResponse { IsValid = false, ErrorMessage = "验证失败" };
        }
        catch (Exception ex)
        {
            return new ValidateProviderResponse { IsValid = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public async Task<ValidateProviderResponse> TestSearchProviderConnectionAsync(CreateSearchProviderRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/v1/providers/search/test", request);
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<ValidateProviderResponse>()
                ?? new ValidateProviderResponse { IsValid = false, ErrorMessage = "测试失败" };
        }
        catch (Exception ex)
        {
            return new ValidateProviderResponse { IsValid = false, ErrorMessage = ex.Message };
        }
    }

    #endregion

    #region Storage 供应商管理

    /// <inheritdoc />
    public async Task<List<StorageProviderResponse>> GetStorageProvidersAsync(bool includeDisabled = false)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/v1/providers/storage?includeDisabled={includeDisabled}");
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<List<StorageProviderResponse>>() ?? new();
        }
        catch
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<StorageProviderResponse> CreateStorageProviderAsync(CreateStorageProviderRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/v1/providers/storage", request);
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<StorageProviderResponse>()
                ?? throw new ApiException("创建 Storage 供应商失败");
        }
        catch
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SetDefaultStorageProviderAsync(Guid providerId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/v1/providers/storage/{providerId}/set-default", null);
            await EnsureSuccessAsync(response);
        }
        catch
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<StorageProviderResponse> UpdateStorageProviderAsync(Guid id, UpdateStorageProviderRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/v1/providers/storage/{id}", request);
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<StorageProviderResponse>()
                ?? throw new ApiException("更新 Storage 供应商失败");
        }
        catch
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteStorageProviderAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/v1/providers/storage/{id}");
            await EnsureSuccessAsync(response);
        }
        catch
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ValidateProviderResponse> ValidateStorageProviderAsync(Guid providerId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/v1/providers/storage/{providerId}/validate", null);
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<ValidateProviderResponse>()
                ?? new ValidateProviderResponse { IsValid = false, ErrorMessage = "验证失败" };
        }
        catch (Exception ex)
        {
            return new ValidateProviderResponse { IsValid = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc />
    public async Task<ValidateProviderResponse> TestStorageProviderConnectionAsync(CreateStorageProviderRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/v1/providers/storage/test", request);
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<ValidateProviderResponse>()
                ?? new ValidateProviderResponse { IsValid = false, ErrorMessage = "测试失败" };
        }
        catch (Exception ex)
        {
            return new ValidateProviderResponse { IsValid = false, ErrorMessage = ex.Message };
        }
    }

    #endregion

    #region 模型定价管理

    /// <inheritdoc />
    public async Task<List<ModelPricingResponse>> GetModelPricingsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/v1/model-pricing");
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<List<ModelPricingResponse>>() ?? new();
        }
        catch
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ModelPricingResponse> CreateModelPricingAsync(CreateModelPricingRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/v1/model-pricing", request);
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<ModelPricingResponse>()
                ?? throw new ApiException("创建模型定价失败");
        }
        catch
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ModelPricingResponse> UpdateModelPricingAsync(Guid id, UpdateModelPricingRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/v1/model-pricing/{id}", request);
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<ModelPricingResponse>()
                ?? throw new ApiException("更新模型定价失败");
        }
        catch
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteModelPricingAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/v1/model-pricing/{id}");
            await EnsureSuccessAsync(response);
        }
        catch
        {
            throw;
        }
    }

    #endregion
}

