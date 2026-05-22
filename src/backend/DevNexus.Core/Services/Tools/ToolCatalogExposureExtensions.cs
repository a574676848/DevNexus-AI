using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using System.Text;

namespace DevNexus.Core.Services.Tools;

/// <summary>
/// 工具目录暴露策略扩展。
/// </summary>
public static class ToolCatalogExposureExtensions
{
    private static readonly char[] InvocationNameSeparators = ['.', '_', '-', ' '];

    /// <summary>
    /// 筛选可直接暴露给普通会话的工具。
    /// </summary>
    public static IReadOnlyList<ToolCatalogItemDto> DirectTools(this IEnumerable<ToolCatalogItemDto> tools)
    {
        return FilterByExposureMode(tools, AiOptimizationConstants.ToolExposureModes.Direct);
    }

    /// <summary>
    /// 筛选需要按 Skill 或领域场景延迟暴露的工具。
    /// </summary>
    public static IReadOnlyList<ToolCatalogItemDto> DeferredTools(this IEnumerable<ToolCatalogItemDto> tools)
    {
        return FilterByExposureMode(tools, AiOptimizationConstants.ToolExposureModes.Deferred);
    }

    /// <summary>
    /// 将请求中的工具名解析为目录中的规范插件名。
    /// </summary>
    public static string? ResolvePluginName(
        this IEnumerable<ToolCatalogItemDto> tools,
        string? requestedName)
    {
        if (string.IsNullOrWhiteSpace(requestedName))
        {
            return null;
        }

        var matches = tools
            .Where(tool => IsNameMatch(tool, requestedName))
            .Select(tool => tool.PluginName)
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    private static IReadOnlyList<ToolCatalogItemDto> FilterByExposureMode(
        IEnumerable<ToolCatalogItemDto> tools,
        string exposureMode)
    {
        return tools
            .Where(tool => tool.ExposureMode == exposureMode)
            .OrderBy(tool => tool.PluginName, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsNameMatch(ToolCatalogItemDto tool, string requestedName)
    {
        return IsCandidateNameMatch(tool.PluginName, requestedName)
            || IsCandidateNameMatch(tool.DisplayName, requestedName)
            || tool.Aliases.Any(alias => IsCandidateNameMatch(alias, requestedName));
    }

    private static bool IsCandidateNameMatch(string candidateName, string requestedName)
    {
        var normalizedCandidateName = NormalizeToolName(candidateName);
        var normalizedRequestedName = NormalizeToolName(requestedName);
        if (normalizedCandidateName == normalizedRequestedName)
        {
            return true;
        }

        return IsInvocationNamePrefix(
            NormalizeInvocationName(requestedName: requestedName),
            NormalizeInvocationName(candidateName: candidateName));
    }

    private static bool IsInvocationNamePrefix(string normalizedRequestedName, string normalizedCandidateName)
    {
        if (normalizedCandidateName.Length == 0
            || normalizedRequestedName.Length <= normalizedCandidateName.Length
            || !normalizedRequestedName.StartsWith(
                $"{normalizedCandidateName}.",
                StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static string NormalizeToolName(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var character in name)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static string NormalizeInvocationName(string requestedName = "", string candidateName = "")
    {
        var name = string.IsNullOrEmpty(candidateName) ? requestedName : candidateName;
        var builder = new StringBuilder(name.Length);
        var previousWasSeparator = false;
        foreach (var character in name)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToUpperInvariant(character));
                previousWasSeparator = false;
                continue;
            }

            if (InvocationNameSeparators.Contains(character) && !previousWasSeparator)
            {
                builder.Append('.');
                previousWasSeparator = true;
            }
        }

        return builder.ToString().Trim('.');
    }
}
