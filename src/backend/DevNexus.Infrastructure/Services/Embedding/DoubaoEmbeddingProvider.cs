// using DevNexus.Domain.Abstractions via GlobalUsings
// using DevNexus.Domain.Configuration via GlobalUsings
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DevNexus.Infrastructure.Services.Embedding;

/// <summary>
/// 豆包 (Doubao) Embedding 提供商
/// 支持文本 Embedding 和多模态 Embedding（文本 + 图片）
/// API 文档: https://www.volcengine.com/docs/82379/1399454
/// </summary>
public class DoubaoEmbeddingProvider : EmbeddingProviderBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public override string ProviderName => "Doubao";

    public DoubaoEmbeddingProvider(
        IHttpClientFactory httpClientFactory,
        EmbeddingProviderConfig config,
        ILogger<DoubaoEmbeddingProvider> logger,
        Guid providerKey,
        string httpClientName = HttpClientNames.DoubaoEmbedding)
        : base(httpClientFactory, config, logger, httpClientName, providerKey)
    {
    }

    /// <summary>
    /// 生成文本向量（使用豆包多模态 Embedding 端点 /embeddings/multimodal）
    /// </summary>
    public override async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or empty", nameof(text));
        }

        try
        {
            var request = new DoubaoMultimodalEmbeddingRequest
            {
                Model = Config.Model,
                Input = [new DoubaoTextInput { Text = text }]
            };

            Logger.LogDebug(
                "[Embedding.Doubao] Generating text embedding | TextLength={Length} | Model={Model}",
                text.Length,
                Config.Model
            );

            var jsonContent = JsonSerializer.Serialize(request, JsonOptions);
            using var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            var response = await SendRequestAsync("embeddings/multimodal", content, cancellationToken);

            var result = await ParseResponseAsync<DoubaoMultimodalEmbeddingResponse>(response, cancellationToken);
            var embedding = result.Data.Embedding;

            LogVectorSizeMismatch(embedding.Length);
            LogEmbeddingGenerated(embedding.Length, result.Usage?.TotalTokens);

            return new ReadOnlyMemory<float>(embedding);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[Embedding.Doubao.Error] Failed to generate text embedding | TextLength={Length}", text.Length);
            throw;
        }
    }

    /// <summary>
    /// 批量生成文本向量（逐条调用豆包多模态端点）
    /// 豆包多模态 API 的 input 是单次请求的多模态内容，不支持 batch，因此逐条调用
    /// </summary>
    public override async Task<IList<ReadOnlyMemory<float>>> GenerateBatchEmbeddingsAsync(
        IList<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (texts == null || texts.Count == 0)
        {
            throw new ArgumentException("Texts list cannot be null or empty", nameof(texts));
        }

        try
        {
            Logger.LogDebug(
                "[Embedding.Doubao] Generating batch text embeddings | Count={Count} | Model={Model}",
                texts.Count,
                Config.Model
            );

            var embeddings = new List<ReadOnlyMemory<float>>(texts.Count);
            foreach (var text in texts)
            {
                var embedding = await GenerateEmbeddingAsync(text, cancellationToken);
                embeddings.Add(embedding);
            }

            Logger.LogDebug(
                "[Embedding.Doubao] Batch text embeddings generated | Count={Count}",
                embeddings.Count
            );

            return embeddings;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[Embedding.Doubao.Error] Failed to generate batch text embeddings | Count={Count}", texts.Count);
            throw;
        }
    }
}
