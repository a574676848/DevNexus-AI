namespace DevNexus.Infrastructure.Services.Search;

/// <summary>
/// Tavily 接口地址解析器
/// 统一处理搜索与验证场景下的 endpoint 规范化，避免一处修复另一处遗漏。
/// </summary>
internal static class TavilyApiUrlResolver
{
    private const string DefaultTavilyApiBaseUrl = "https://api.tavily.com";

    public static string GetSearchUrl(string? endpoint)
    {
        var raw = string.IsNullOrWhiteSpace(endpoint)
            ? DefaultTavilyApiBaseUrl
            : endpoint.Trim();

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            return $"{raw.TrimEnd('/')}/search";
        }

        var path = uri.AbsolutePath.Trim('/');
        if (string.Equals(path, "search", StringComparison.OrdinalIgnoreCase))
        {
            return uri.GetLeftPart(UriPartial.Path);
        }

        if (string.IsNullOrEmpty(path))
        {
            return $"{uri.GetLeftPart(UriPartial.Authority)}/search";
        }

        // 自定义代理路径：优先尊重用户配置，不强制追加 /search
        return uri.GetLeftPart(UriPartial.Path);
    }
}
