using DevNexus.Domain.Abstractions;
using DevNexus.Infrastructure.Services.LLM;
using DevNexus.Infrastructure.Services.Memory;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace DevNexus.Infrastructure.Services.Embedding;

/// <summary>
/// Embedding 提供商审计装饰器。
/// </summary>
public class AuditedEmbeddingProviderDecorator : IEmbeddingProvider
{
    private readonly IEmbeddingProvider _inner;
    private readonly ITokenAuditService _tokenAuditService;
    private readonly ILogger<AuditedEmbeddingProviderDecorator> _logger;

    public AuditedEmbeddingProviderDecorator(
        IEmbeddingProvider inner,
        ITokenAuditService tokenAuditService,
        ILogger<AuditedEmbeddingProviderDecorator> logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _tokenAuditService = tokenAuditService ?? throw new ArgumentNullException(nameof(tokenAuditService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string ProviderName => _inner.ProviderName;

    /// <inheritdoc />
    public Guid ProviderKey => _inner.ProviderKey;

    /// <inheritdoc />
    public string ModelName => _inner.ModelName;

    /// <inheritdoc />
    public int VectorSize => _inner.VectorSize;

    /// <inheritdoc />
    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await _inner.GenerateEmbeddingAsync(text, cancellationToken);
            stopwatch.Stop();

            RecordAudit(
                inputTokens: TiktokenService.CountTokens(text),
                outputTokens: 0,
                durationMs: stopwatch.ElapsedMilliseconds,
                status: ModelInvocationStatuses.Succeeded,
                usageSource: ModelInvocationUsageSources.Estimated,
                meteringValue: 1m);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RecordAudit(
                inputTokens: TiktokenService.CountTokens(text),
                outputTokens: 0,
                durationMs: stopwatch.ElapsedMilliseconds,
                status: ModelInvocationStatuses.Failed,
                usageSource: ModelInvocationUsageSources.Estimated,
                meteringValue: 1m,
                errorCode: ex.GetType().Name,
                errorMessage: ex.Message);

            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IList<ReadOnlyMemory<float>>> GenerateBatchEmbeddingsAsync(
        IList<string> texts,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await _inner.GenerateBatchEmbeddingsAsync(texts, cancellationToken);
            stopwatch.Stop();

            RecordAudit(
                inputTokens: texts.Sum(TiktokenService.CountTokens),
                outputTokens: 0,
                durationMs: stopwatch.ElapsedMilliseconds,
                status: ModelInvocationStatuses.Succeeded,
                usageSource: ModelInvocationUsageSources.Estimated,
                meteringValue: texts.Count);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RecordAudit(
                inputTokens: texts.Sum(TiktokenService.CountTokens),
                outputTokens: 0,
                durationMs: stopwatch.ElapsedMilliseconds,
                status: ModelInvocationStatuses.Failed,
                usageSource: ModelInvocationUsageSources.Estimated,
                meteringValue: texts.Count,
                errorCode: ex.GetType().Name,
                errorMessage: ex.Message);

            throw;
        }
    }

    private void RecordAudit(
        int inputTokens,
        int outputTokens,
        long durationMs,
        string status,
        string usageSource,
        decimal meteringValue,
        string? errorCode = null,
        string? errorMessage = null)
    {
        var ctx = TokenAuditContext.Current;
        _tokenAuditService.RecordStreamingCompletion(
            sessionId: ctx?.SessionId,
            messageId: ctx?.MessageId,
            userId: ctx?.OwnerUserId,
            modelId: ModelName,
            providerName: ProviderName,
            providerType: ModelInvocationProviderTypes.Embedding,
            providerId: ProviderKey.ToString(),
            inputTokens: inputTokens,
            outputTokens: outputTokens,
            durationMs: durationMs,
            invocationKind: ModelInvocationKinds.Embedding,
            sceneCode: ctx?.SceneCode ?? ModelInvocationSceneCodes.KnowledgeEmbeddingIndex,
            sceneCategory: ctx?.SceneCategory ?? ModelInvocationSceneCategories.Memory,
            resourceType: ctx?.ResourceType ?? ModelInvocationResourceTypes.None,
            resourceId: ctx?.ResourceId,
            usageSource: usageSource,
            status: status,
            errorCode: errorCode,
            errorMessage: errorMessage,
            meteringType: ModelInvocationMeteringTypes.Token,
            meteringValue: meteringValue);

        _logger.LogDebug(
            "[Embedding.Audit] Recorded embedding audit | Provider={Provider} Model={Model} SceneCode={SceneCode} Status={Status}",
            ProviderName,
            ModelName,
            ctx?.SceneCode ?? ModelInvocationSceneCodes.KnowledgeEmbeddingIndex,
            status);
    }
}
