using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Configuration;
using DevNexus.Infrastructure.Services.Embedding;
using DevNexus.Infrastructure.Services.LLM;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.KernelMemory.AI;

namespace DevNexus.Infrastructure.Services.Memory;

/// <summary>
/// Kernel Memory Embedding 适配器
/// 将项目现有的 IEmbeddingProvider 适配为 Kernel Memory 的 ITextEmbeddingGenerator 接口
/// 使用 Microsoft.ML.Tokenizers 实现精确的 BPE 分词
/// 
/// 注意：虽然此适配器注册为 Singleton，但它通过 IServiceScopeFactory 创建新作用域来解析
/// Scoped 的 IEmbeddingProviderFactory，避免了 DI 生命周期冲突。
/// </summary>
public class KernelMemoryEmbeddingAdapter : ITextEmbeddingGenerator
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ulong _qdrantVectorSize;
    private readonly ILogger<KernelMemoryEmbeddingAdapter> _logger;

    public KernelMemoryEmbeddingAdapter(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<QdrantOptions> qdrantOptions,
        ILogger<KernelMemoryEmbeddingAdapter> logger)
    {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _qdrantVectorSize = qdrantOptions?.Value?.VectorSize ?? 0;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 最大 Token 限制（cl100k_base 编码）
    /// </summary>
    public int MaxTokens => 8192;

    /// <summary>
    /// 精确计算文本的 Token 数量
    /// 使用 tiktoken cl100k_base 编码（GPT-4/text-embedding-ada-002）
    /// </summary>
    public int CountTokens(string text) => TiktokenService.CountTokens(text);

    /// <summary>
    /// 获取 Token 列表（返回 Token ID 的字符串表示）
    /// 使用 tiktoken cl100k_base 编码
    /// </summary>
    public IReadOnlyList<string> GetTokens(string text)
    {
        var tokenIds = TiktokenService.Encode(text);
        return tokenIds.Select(id => id.ToString()).ToList();
    }

    /// <summary>
    /// 生成文本嵌入向量
    /// 通过 IServiceScopeFactory 创建新作用域来解析 IEmbeddingProviderFactory，避免 DI 生命周期冲突
    /// </summary>
    public async Task<Microsoft.KernelMemory.Embedding> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var previousContext = TokenAuditContext.Current;
        // 创建新的作用域来解析 Scoped 服务
        using var scope = _serviceScopeFactory.CreateScope();
        var embeddingFactory = scope.ServiceProvider.GetRequiredService<IEmbeddingProviderFactory>();
        var provider = embeddingFactory.GetDefaultProvider();
        try
        {
            TokenAuditContext.Current ??= new TokenAuditContext
            {
                OwnerType = ModelInvocationOwnerTypes.System,
                InvocationKind = ModelInvocationKinds.Embedding,
                SceneCode = ModelInvocationSceneCodes.KnowledgeEmbeddingIndex,
                SceneCategory = ModelInvocationSceneCategories.Memory,
                ResourceType = ModelInvocationResourceTypes.None
            };

            var vector = await GenerateKernelMemoryEmbeddingAsync(provider, text, cancellationToken);
            return new Microsoft.KernelMemory.Embedding(NormalizeVectorSize(vector.Span).ToArray());
        }
        finally
        {
            TokenAuditContext.Current = previousContext;
        }
    }

    private async Task<ReadOnlyMemory<float>> GenerateKernelMemoryEmbeddingAsync(
        IEmbeddingProvider provider,
        string text,
        CancellationToken cancellationToken)
    {
        if (_qdrantVectorSize == 0 || provider.VectorSize <= 0 || provider.VectorSize == (int)_qdrantVectorSize)
        {
            return await provider.GenerateEmbeddingAsync(text, cancellationToken);
        }

        if (provider is OpenAIEmbeddingProvider openAIProvider)
        {
            return await openAIProvider.GenerateEmbeddingAsync(text, (int)_qdrantVectorSize, cancellationToken);
        }

        return await provider.GenerateEmbeddingAsync(text, cancellationToken);
    }

    /// <summary>
    /// 将向量长度标准化为当前 Qdrant 集合维度，避免供应商忽略 dimensions 参数时写入失败。
    /// </summary>
    private ReadOnlySpan<float> NormalizeVectorSize(ReadOnlySpan<float> vector)
    {
        if (_qdrantVectorSize == 0 || vector.Length == (int)_qdrantVectorSize)
        {
            return vector;
        }

        var normalized = new float[_qdrantVectorSize];
        var copyLength = Math.Min(vector.Length, normalized.Length);
        vector[..copyLength].CopyTo(normalized);

        _logger.LogWarning(
            "[KernelMemoryEmbeddingAdapter] 向量维度已标准化 | Expected={Expected} | Actual={Actual} | Copied={Copied}",
            _qdrantVectorSize,
            vector.Length,
            copyLength);

        return normalized;
    }
}
