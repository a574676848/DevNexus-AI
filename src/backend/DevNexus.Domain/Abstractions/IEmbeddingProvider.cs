namespace DevNexus.Domain.Abstractions;

/// <summary>
/// Embedding 提供商抽象接口
/// 支持多提供商切换（豆包 / OpenAI / 本地模型）
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// 提供商名称
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// 提供商数据库主键 ID。
    /// </summary>
    Guid ProviderKey { get; }

    /// <summary>
    /// 模型名称。
    /// </summary>
    string ModelName { get; }

    /// <summary>
    /// 向量维度大小
    /// </summary>
    int VectorSize { get; }

    /// <summary>
    /// 生成文本向量
    /// </summary>
    /// <param name="text">要生成向量的文本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>向量数组</returns>
    Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量生成文本向量
    /// </summary>
    /// <param name="texts">要生成向量的文本列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>向量数组列表</returns>
    Task<IList<ReadOnlyMemory<float>>> GenerateBatchEmbeddingsAsync(
        IList<string> texts,
        CancellationToken cancellationToken = default);
}
