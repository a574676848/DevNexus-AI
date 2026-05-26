namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 网络资源的工具路由规则。
/// </summary>
public static class WebResourceRoutingPolicy
{
    /// <summary>
    /// Git 仓库不应进入通用网页阅读器时返回的稳定提示。
    /// </summary>
    public const string GitRepositoryReaderError =
        "Git 仓库 URL 不支持网页阅读，请使用 repo-parser Skill 解析仓库。";

    private static readonly HashSet<string> RepositoryHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "github.com",
        "gitlab.com",
        "gitea.com",
        "gitingest.com",
        "bitbucket.org"
    };

    /// <summary>
    /// 判断 URL 是否指向已识别托管站点上的仓库路径。
    /// </summary>
    public static bool IsGitRepositoryUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || !RepositoryHosts.Contains(uri.Host))
        {
            return false;
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return segments.Length >= 2;
    }
}
