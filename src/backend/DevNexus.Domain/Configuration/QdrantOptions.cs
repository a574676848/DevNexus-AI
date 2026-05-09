namespace DevNexus.Domain.Configuration;

/// <summary>
/// Qdrant 向量数据库配置选项
/// </summary>
public class QdrantOptions
{
    /// <summary>
    /// 集合名称（代码索引用）
    /// </summary>
    public string CollectionName { get; set; } = "devnexus_code";

    /// <summary>
    /// 用户情境记忆集合名称
    /// </summary>
    public string MemoryCollectionName { get; set; } = "user_episodic_memory";

    /// <summary>
    /// 向量维度大小
    /// </summary>
    public ulong VectorSize { get; set; } = 1024;

    /// <summary>
    /// 距离度量方式 (Cosine, Euclid, Dot)
    /// </summary>
    public string Distance { get; set; } = "Cosine";

    /// <summary>
    /// 是否使用 HTTPS
    /// </summary>
    public bool UseHttps { get; set; } = false;

    /// <summary>
    /// API Key（如果需要）
    /// </summary>
    public string? ApiKey { get; set; }
    
    /// <summary>
    /// 从连接字符串解析的主机地址（内部使用）
    /// </summary>
    public string? Host { get; set; }
    
    /// <summary>
    /// 从连接字符串解析的端口（内部使用）
    /// </summary>
    public int Port { get; set; } = 6334;
}
