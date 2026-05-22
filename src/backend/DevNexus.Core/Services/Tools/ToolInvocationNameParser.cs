namespace DevNexus.Core.Services.Tools;

/// <summary>
/// 工具调用名称解析器。
/// </summary>
public static class ToolInvocationNameParser
{
    private static readonly HashSet<string> WrapperPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "function",
        "functions",
        "tool",
        "tools"
    };

    /// <summary>
    /// 解析工具调用名称。
    /// </summary>
    public static ToolInvocationNameParts Parse(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return new ToolInvocationNameParts();
        }

        var segments = toolName
            .Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();
        if (segments.Length == 0)
        {
            return new ToolInvocationNameParts();
        }

        var offset = WrapperPrefixes.Contains(segments[0]) && segments.Length > 1 ? 1 : 0;
        return new ToolInvocationNameParts
        {
            PluginName = segments[offset],
            FunctionName = offset + 1 < segments.Length ? segments[offset + 1] : null,
            RawName = toolName.Trim()
        };
    }
}

/// <summary>
/// 工具调用名称解析结果。
/// </summary>
public sealed record ToolInvocationNameParts
{
    /// <summary>
    /// 原始工具名称。
    /// </summary>
    public string RawName { get; init; } = string.Empty;

    /// <summary>
    /// 插件名称。
    /// </summary>
    public string PluginName { get; init; } = string.Empty;

    /// <summary>
    /// 函数名称。
    /// </summary>
    public string? FunctionName { get; init; }
}
