namespace DevNexus.Domain.Configuration;

/// <summary>
/// Embedding 配置选项
/// </summary>
public class EmbeddingOptions
{
    /// <summary>
    /// 默认提供商
    /// </summary>
    public string DefaultProvider { get; set; } = "Doubao";

    /// <summary>
    /// 提供商配置字典
    /// </summary>
    public Dictionary<string, EmbeddingProviderConfig> Providers { get; set; } = new();
}

/// <summary>
/// Embedding 提供商配置
/// </summary>
public class EmbeddingProviderConfig
{
    /// <summary>
    /// 服务地址
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// 模型名称
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// API 密钥
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// 向量维度（用于验证）
    /// </summary>
    public int VectorSize { get; set; } = 1024;

    /// <summary>
    /// 请求超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 批量处理大小
    /// </summary>
    public int BatchSize { get; set; } = 10;
}
