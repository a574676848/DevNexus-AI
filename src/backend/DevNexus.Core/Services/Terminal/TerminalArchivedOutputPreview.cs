namespace DevNexus.Core.Services.Terminal;

/// <summary>
/// 终端归档输出预览裁剪器。
/// </summary>
public static class TerminalArchivedOutputPreview
{
    private const int MaxPreviewChars = 120_000;
    private const int TargetPreviewChars = 90_000;
    private const string PreviewTrimBanner = "[较早输出已归档，当前仅保留最近内容]";

    /// <summary>
    /// 为数据库预览字段保留最近输出，完整内容仍以归档文件为事实源。
    /// </summary>
    public static string Normalize(string output)
    {
        if (string.IsNullOrEmpty(output) || output.Length <= MaxPreviewChars)
        {
            return output;
        }

        var startIndex = Math.Max(0, output.Length - TargetPreviewChars);
        var lineBreakIndex = output.IndexOf('\n', startIndex);
        if (lineBreakIndex >= 0 && lineBreakIndex < output.Length - 1)
        {
            startIndex = lineBreakIndex + 1;
        }

        var suffix = output[startIndex..];
        return $"{PreviewTrimBanner}{Environment.NewLine}{suffix}";
    }

    /// <summary>
    /// 移除预览裁剪标记，便于继续拼接新的输出增量后重新裁剪。
    /// </summary>
    public static string StripBanner(string output)
    {
        if (string.IsNullOrEmpty(output))
        {
            return string.Empty;
        }

        var prefix = $"{PreviewTrimBanner}{Environment.NewLine}";
        return output.StartsWith(prefix, StringComparison.Ordinal)
            ? output[prefix.Length..]
            : output;
    }
}
