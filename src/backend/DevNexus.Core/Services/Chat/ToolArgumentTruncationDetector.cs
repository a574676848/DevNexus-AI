namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 工具参数截断特征检测器。
/// </summary>
internal static class ToolArgumentTruncationDetector
{
    private static readonly string[] ParameterTagMarkers =
    [
        "<parameter",
        "</parameter>"
    ];

    /// <summary>
    /// 判断参数文本是否更像上游截断或 Provider 标签污染，而不是普通非法 JSON。
    /// </summary>
    public static bool LooksTruncated(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return true;
        }

        var text = arguments.Trim();
        return HasParameterTagContamination(text) || HasUnbalancedJsonContainer(text);
    }

    private static bool HasParameterTagContamination(string text)
    {
        return ParameterTagMarkers.Any(marker =>
            text.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasUnbalancedJsonContainer(string text)
    {
        if (!text.StartsWith('{') && !text.StartsWith('['))
        {
            return false;
        }

        return CountOutsideString(text, '{') > CountOutsideString(text, '}')
            || CountOutsideString(text, '[') > CountOutsideString(text, ']');
    }

    private static int CountOutsideString(string text, char target)
    {
        var count = 0;
        var inString = false;
        var escaped = false;

        foreach (var current in text)
        {
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (current == '\\' && inString)
            {
                escaped = true;
                continue;
            }

            if (current == '"')
            {
                inString = !inString;
                continue;
            }

            if (!inString && current == target)
            {
                count++;
            }
        }

        return count;
    }
}
