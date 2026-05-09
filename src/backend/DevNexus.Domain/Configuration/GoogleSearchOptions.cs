namespace DevNexus.Domain.Configuration;

/// <summary>
/// Google Custom Search API 配置选项
/// </summary>
public class GoogleSearchOptions
{
    /// <summary>
    /// Google Custom Search API Key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Google Custom Search Engine ID (CX)
    /// </summary>
    public string SearchEngineId { get; set; } = string.Empty;

    /// <summary>
    /// Google Custom Search API 端点
    /// </summary>
    public string Endpoint { get; set; } = "https://www.googleapis.com/customsearch/v1";

    /// <summary>
    /// 默认搜索结果数量
    /// </summary>
    public int DefaultCount { get; set; } = 10;

    /// <summary>
    /// 搜索语言
    /// </summary>
    public string Language { get; set; } = "zh-CN";

    /// <summary>
    /// 搜索安全级别 (off, medium, high)
    /// </summary>
    public string SafeSearch { get; set; } = "medium";
}
