namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 工具输出预算压缩器。
/// </summary>
public static class ToolOutputBudgetCompressor
{
    private const string OmittedMarker = "\n...[已按模型可见预算省略中间内容]...\n";

    /// <summary>
    /// 按字符预算压缩模型可见文本，保留头尾上下文。
    /// </summary>
    public static string Compress(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || maxLength <= 0)
        {
            return string.Empty;
        }

        if (value.Length <= maxLength)
        {
            return value;
        }

        var metadata = BuildMetadata(value);
        var available = maxLength - metadata.Length - OmittedMarker.Length;
        if (available <= 0)
        {
            return value[..maxLength];
        }

        var headLength = Math.Max(1, available / 2);
        var tailLength = Math.Max(1, available - headLength);

        return metadata +
               value[..headLength] +
               OmittedMarker +
               value[^tailLength..];
    }

    private static string BuildMetadata(string value)
    {
        return $"Total output chars: {value.Length}\n" +
               $"Total output lines: {CountLines(value)}\n\n";
    }

    private static int CountLines(string value)
    {
        if (value.Length == 0)
        {
            return 0;
        }

        return value.Count(character => character == '\n') + 1;
    }
}
