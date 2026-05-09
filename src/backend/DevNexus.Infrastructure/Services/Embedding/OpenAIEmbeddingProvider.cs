// using DevNexus.Domain.Abstractions via GlobalUsings
// using DevNexus.Domain.Configuration via GlobalUsings
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using DevNexus.Shared.Constants;

namespace DevNexus.Infrastructure.Services.Embedding;

/// <summary>
/// OpenAI Embedding 提供商
/// 支持 text-embedding-3-small / text-embedding-3-large
/// </summary>
public class OpenAIEmbeddingProvider : EmbeddingProviderBase
{
    public override string ProviderName => "OpenAI";

    private bool SupportsCustomDimensions =>
        Config.Model.StartsWith("text-embedding-3", StringComparison.OrdinalIgnoreCase);

    private int? RequestedDimensions =>
        SupportsCustomDimensions && Config.VectorSize > 0 ? Config.VectorSize : null;

    public OpenAIEmbeddingProvider(
        IHttpClientFactory httpClientFactory,
        EmbeddingProviderConfig config,
        ILogger<OpenAIEmbeddingProvider> logger,
        Guid providerKey,
        string httpClientName = HttpClientNames.OpenAIEmbedding)
        : base(httpClientFactory, config, logger, httpClientName, providerKey)
    {
    }

    public override async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        return await GenerateEmbeddingAsync(text, RequestedDimensions, cancellationToken);
    }

    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string text,
        int? dimensions,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or empty", nameof(text));
        }

        try
        {
            var request = new OpenAIEmbeddingRequest
            {
                Model = Config.Model,
                Input = text,
                EncodingFormat = "float",
                Dimensions = ResolveDimensions(dimensions)
            };

            Logger.LogDebug(
                "[Embedding.OpenAI] Generating embedding | TextLength={Length} | Model={Model}",
                text.Length,
                Config.Model
            );

            var jsonContent = JsonSerializer.Serialize(request);
            using var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            var response = await SendRequestAsync("embeddings", content, cancellationToken);

            var result = await ParseResponseAsync<OpenAIEmbeddingResponse>(response, cancellationToken);
            var embedding = result.Data[0].Embedding;

            LogVectorSizeMismatch(embedding.Length);
            LogEmbeddingGenerated(embedding.Length, result.Usage?.TotalTokens);

            return new ReadOnlyMemory<float>(embedding);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[Embedding.OpenAI.Error] Failed to generate embedding | TextLength={Length}", text.Length);
            throw;
        }
    }

    public override async Task<IList<ReadOnlyMemory<float>>> GenerateBatchEmbeddingsAsync(
        IList<string> texts,
        CancellationToken cancellationToken = default)
    {
        return await GenerateBatchEmbeddingsAsync(texts, RequestedDimensions, cancellationToken);
    }

    public async Task<IList<ReadOnlyMemory<float>>> GenerateBatchEmbeddingsAsync(
        IList<string> texts,
        int? dimensions,
        CancellationToken cancellationToken = default)
    {
        if (texts == null || texts.Count == 0)
        {
            throw new ArgumentException("Texts list cannot be null or empty", nameof(texts));
        }

        try
        {
            var request = new OpenAIEmbeddingRequest
            {
                Model = Config.Model,
                Input = texts.ToArray(),
                EncodingFormat = "float",
                Dimensions = ResolveDimensions(dimensions)
            };

            Logger.LogDebug(
                "[Embedding.OpenAI] Generating batch embeddings | Count={Count} | Model={Model}",
                texts.Count,
                Config.Model
            );

            var jsonContent = JsonSerializer.Serialize(request);
            using var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
            var response = await SendRequestAsync("embeddings", content, cancellationToken);

            var result = await ParseResponseAsync<OpenAIEmbeddingResponse>(response, cancellationToken);
            var embeddings = result.Data
                .OrderBy(d => d.Index)
                .Select(d => new ReadOnlyMemory<float>(d.Embedding))
                .ToList();

            Logger.LogDebug(
                "[Embedding.OpenAI] Batch embeddings generated | Count={Count} | Usage={Usage}",
                embeddings.Count,
                result.Usage?.TotalTokens ?? 0
            );

            return embeddings;
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "[Embedding.OpenAI.Error] Failed to generate batch embeddings | Count={Count}",
                texts.Count
            );
            throw;
        }
    }

    private int? ResolveDimensions(int? dimensions)
    {
        if (!SupportsCustomDimensions)
        {
            return null;
        }

        return dimensions is > 0 ? dimensions : RequestedDimensions;
    }
}
