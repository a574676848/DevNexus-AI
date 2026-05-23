namespace DevNexus.Core.Services.Terminal;

/// <summary>
/// 终端保留缓冲切片工具。
/// </summary>
public static class TerminalRetainedOutputSlice
{
    /// <summary>
    /// 内存水位触发后写入保留缓冲开头的裁剪标记。
    /// </summary>
    public const string TrimmedHistoryMarker = "... (由于内存安全策略已截断历史输出)\n";

    /// <summary>
    /// 根据旧偏移量读取当前保留缓冲中的增量输出。
    /// </summary>
    public static string FromRetainedBuffer(string retainedOutput, int startIndex)
    {
        if (string.IsNullOrEmpty(retainedOutput))
        {
            return string.Empty;
        }

        if (startIndex <= 0)
        {
            return retainedOutput;
        }

        if (startIndex < retainedOutput.Length)
        {
            return retainedOutput[startIndex..];
        }

        return retainedOutput.StartsWith(TrimmedHistoryMarker, StringComparison.Ordinal)
            && startIndex >= retainedOutput.Length
            ? retainedOutput
            : string.Empty;
    }
}
