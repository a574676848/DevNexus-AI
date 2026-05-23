namespace DevNexus.Core.Services.Terminal;

/// <summary>
/// 终端日志分块输出切片工具。
/// </summary>
public static class TerminalLogChunkOutputSlice
{
    /// <summary>
    /// 根据输出来源返回日志分块内容。
    /// </summary>
    public static (string Output, string PlainOutput) FromSources(
        string archivedOutput,
        string archivedPlainOutput,
        string liveOutput,
        string livePlainOutput,
        int startIndex,
        bool useArchivedOutput)
    {
        if (!useArchivedOutput)
        {
            return (liveOutput, livePlainOutput);
        }

        return (SliceArchivedOutput(archivedOutput, startIndex), SliceArchivedOutput(archivedPlainOutput, startIndex));
    }

    private static string SliceArchivedOutput(string output, int startIndex)
    {
        if (string.IsNullOrEmpty(output))
        {
            return string.Empty;
        }

        if (startIndex <= 0)
        {
            return output;
        }

        return startIndex < output.Length ? output[startIndex..] : string.Empty;
    }
}
