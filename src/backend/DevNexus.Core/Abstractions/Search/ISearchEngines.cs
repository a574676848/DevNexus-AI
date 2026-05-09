using DevNexus.Domain.Entities;

namespace DevNexus.Core.Abstractions.Search;

/// <summary>
/// 统一搜索引擎接口 (返回 URL 列表)
/// </summary>
public interface ISearchEngine
{
    /// <summary>
    /// 获取搜索到的 URL 列表
    /// </summary>
    /// <param name="query">关键词</param>
    /// <param name="count">预期返回条数</param>
    /// <param name="config">供应商配置信息</param>
    /// <returns>URL 列表</returns>
    Task<List<string>> SearchUrlsAsync(string query, int count, SearchProvider config);
}

/// <summary>
/// 统一网页阅读/解析接口 (返回 Markdown)
/// </summary>
public interface IWebReaderEngine
{
    /// <summary>
    /// 读取网页并转换为 Markdown
    /// </summary>
    /// <param name="url">目标 URL</param>
    /// <param name="config">供应商配置信息</param>
    /// <returns>Markdown 内容</returns>
    Task<string> ReadWebpageAsync(string url, SearchProvider config);
}
