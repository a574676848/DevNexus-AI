namespace DevNexus.Domain.Configuration;

/// <summary>
/// Elasticsearch 配置选项
/// </summary>
public class ElasticsearchOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Elasticsearch";

    /// <summary>
    /// 服务地址 (例如: http://localhost:9200)
    /// </summary>
    public string Url { get; set; } = "http://localhost:9200";

    /// <summary>
    /// 用户名（可选）
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// 密码（可选）
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// API Key（可选，优先于用户名密码）
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// 会话索引名称
    /// </summary>
    public string SessionIndexName { get; set; } = "devnexus-sessions";

    /// <summary>
    /// 消息索引名称
    /// </summary>
    public string MessageIndexName { get; set; } = "devnexus-messages";

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; } = true;
}
