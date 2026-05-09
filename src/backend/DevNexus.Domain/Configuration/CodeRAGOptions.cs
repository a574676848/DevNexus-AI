namespace DevNexus.Domain.Configuration;

/// <summary>
/// 代码 RAG 配置选项
/// </summary>
public class CodeRAGOptions
{
    /// <summary>
    /// 语义搜索相似度阈值（0.0-1.0）
    /// </summary>
    public double SimilarityThreshold { get; set; } = 0.7;

    /// <summary>
    /// 搜索结果最大数量
    /// </summary>
    public int MaxSearchResults { get; set; } = 10;

    /// <summary>
    /// 批量索引大小
    /// </summary>
    public int BatchIndexSize { get; set; } = 100;

    /// <summary>
    /// 是否启用缓存
    /// </summary>
    public bool EnableCache { get; set; } = true;

    /// <summary>
    /// 缓存过期时间（分钟）
    /// </summary>
    public int CacheExpirationMinutes { get; set; } = 60;
}
