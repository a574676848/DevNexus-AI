using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;
using DevNexus.Core.Models.Execution;

namespace DevNexus.Infrastructure.Services.Systems;

/// <summary>
/// 宿主服务路径访问与路径重写能力。
/// </summary>
public partial class HostService
{
    private sealed record PathRewriteEntry(string OriginalPath, string RewrittenPath);

    /// <summary>
    /// 获取归一化后的文本及其对应的原始索引映射表。
    /// </summary>
    private static (string Normalized, int[] Map) GetNormalizedWithMap(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return ("", Array.Empty<int>());
        }

        var sb = new StringBuilder();
        var map = new List<int>();

        for (int i = 0; i < input.Length; i++)
        {
            if (!char.IsWhiteSpace(input[i]))
            {
                sb.Append(input[i]);
                map.Add(i);
            }
        }

        return (sb.ToString(), map.ToArray());
    }

    /// <summary>
    /// 构建权限拒绝信息（不暴露沙箱目录细节）。
    /// </summary>
    private static string GetPermissionDeniedMessage(string path)
    {
        return TaggedExecutionText.Failure(
            $"指定路径 '{path}' 不存在或无法访问。请仅在 tmp 或 project 工作区内操作，并优先使用相对路径。");
    }

    /// <summary>
    /// 将命令文本里落在宿主内容根中的 Skill 或静态资源路径重写到当前用户可访问的镜像目录。
    /// </summary>
    private string RewriteAccessiblePathsInCommandText(
        Guid userId,
        string? input,
        List<PathRewriteEntry>? rewrites = null)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input ?? string.Empty;
        }

        var rewritten = new StringBuilder(input.Length);
        var token = new StringBuilder();
        char? quote = null;

        void FlushToken()
        {
            if (token.Length == 0)
            {
                return;
            }

            rewritten.Append(RewritePathFragmentsInToken(userId, token.ToString(), rewrites));
            token.Clear();
        }

        foreach (var ch in input)
        {
            if (quote.HasValue)
            {
                token.Append(ch);
                if (ch == quote.Value)
                {
                    quote = null;
                }

                continue;
            }

            if (ch is '"' or '\'')
            {
                token.Append(ch);
                quote = ch;
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                FlushToken();
                rewritten.Append(ch);
                continue;
            }

            token.Append(ch);
        }

        FlushToken();
        return rewritten.ToString();
    }

    /// <summary>
    /// 在单个命令参数 token 中精确改写路径片段，保留原始分隔符和引号。
    /// </summary>
    private string RewritePathFragmentsInToken(
        Guid userId,
        string token,
        List<PathRewriteEntry>? rewrites = null)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        const string PathFragmentPattern =
            "(?<path>[A-Za-z]:\\\\[^\\s\"';,|]+(?:[\\\\/][^\\s\"';,|]+)*|/[^\\s\"';,|]+(?:/[^\\s\"';,|]+)*)";

        return Regex.Replace(
            token,
            PathFragmentPattern,
            match => RewriteMatchedPath(userId, match.Value, rewrites),
            RegexOptions.CultureInvariant);
    }

    private string RewriteMatchedPath(
        Guid userId,
        string candidatePath,
        List<PathRewriteEntry>? rewrites = null)
    {
        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return candidatePath;
        }

        var sanitizedPath = candidatePath.Trim().TrimEnd(')', ']', '}');
        var trailingSuffix = candidatePath[sanitizedPath.Length..];
        var mirroredPath = _skillRuntimePathResolver.TryResolveAccessiblePath(userId, sanitizedPath);

        if (string.IsNullOrWhiteSpace(mirroredPath))
        {
            return candidatePath;
        }

        if (!string.Equals(sanitizedPath, mirroredPath, StringComparison.OrdinalIgnoreCase))
        {
            rewrites?.Add(new PathRewriteEntry(sanitizedPath, mirroredPath));
        }

        return mirroredPath + trailingSuffix;
    }

    /// <summary>
    /// 构建 CLI 内部会话键。
    /// </summary>
    private static string BuildInternalCliSessionKey(Guid userId, string publicSessionId)
    {
        return $"{userId:N}:{publicSessionId}";
    }

    /// <summary>
    /// 构建工作目录锁键。
    /// </summary>
    private static string BuildCliLockKey(Guid userId, string workingDirectory)
    {
        var normalizedDirectory = Path.GetFullPath(workingDirectory)
            .TrimEnd(Path.DirectorySeparatorChar)
            .ToLowerInvariant();

        return $"{userId:N}:{normalizedDirectory}";
    }

    /// <inheritdoc />
    public bool ValidatePathAccess(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var userId = _userContextAccessor.CurrentUserId;
            if (!userId.HasValue)
            {
                _logger.LogWarning("[HostService] 无用户上下文，拒绝路径访问 | Path={Path}", path);
                return false;
            }

            var isAllowed = _userStoragePathService.ValidateUserPathAccess(userId.Value, path);
            if (!isAllowed)
            {
                _logger.LogWarning(
                    "[HostService] 用户路径访问被拒绝 | UserId={UserId} Path={Path}",
                    userId.Value,
                    path);
            }

            return isAllowed;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
