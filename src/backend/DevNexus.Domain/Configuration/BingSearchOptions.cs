namespace DevNexus.Domain.Configuration;

/// <summary>
/// Bing 搜索 API 配置选项
/// </summary>
public class BingSearchOptions
{
    /// <summary>
    /// Bing Search API Key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Bing Search API 端点
    /// </summary>
    public string Endpoint { get; set; } = "https://api.bing.microsoft.com/v7.0/search";

    /// <summary>
    /// 默认搜索结果数量
    /// </summary>
    public int DefaultCount { get; set; } = 10;

    /// <summary>
    /// 搜索市场/区域
    /// </summary>
    public string Market { get; set; } = "zh-CN";

    /// <summary>
    /// 搜索安全级别 (Off, Moderate, Strict)
    /// </summary>
    public string SafeSearch { get; set; } = "Moderate";
}
