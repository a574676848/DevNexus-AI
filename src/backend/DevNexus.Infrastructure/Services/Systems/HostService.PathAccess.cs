using Microsoft.Extensions.Logging;
using System.Text;
using DevNexus.Core.Models.Execution;

namespace DevNexus.Infrastructure.Services.Systems;

/// <summary>
/// 宿主服务路径访问与路径重写能力。
/// </summary>
public partial class HostService
{
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
            $"指定路径 '{path}' 不存在或无法访问。请确认本机路径存在，并检查服务运行账户的系统权限。");
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

            _ = Path.GetFullPath(path);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[HostService] 路径格式无效 | Path={Path}", path);
            return false;
        }
    }
}
