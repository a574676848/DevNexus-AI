namespace DevNexus.Core.Services.Terminal;

/// <summary>
/// 终端输出预览构建器。
/// </summary>
public static class TerminalOutputPreviewBuilder
{
    private const string OmittedMarker = "\n...[已按终端预览预算省略中间输出]...\n";
    private const string LongLineMarker = "...[单行输出已截断]...";
    private const int DefaultMaxPreviewLines = 120;
    private const int DefaultMaxLineLength = 320;

    /// <summary>
    /// 按头尾预算构建模型可见的终端输出预览，并限制长单行噪音。
    /// </summary>
    public static string Build(string output, int headLimit = 1500, int tailLimit = 3500)
    {
        if (string.IsNullOrEmpty(output))
        {
            return string.Empty;
        }

        var normalizedHeadLimit = Math.Max(0, headLimit);
        var normalizedTailLimit = Math.Max(0, tailLimit);
        var maxLength = normalizedHeadLimit + normalizedTailLimit;
        if (maxLength <= 0)
        {
            return string.Empty;
        }

        var normalizedOutput = NormalizeLines(output);
        var hasLongLine = normalizedOutput.Length != output.Length;
        if (normalizedOutput.Length <= maxLength && !hasLongLine && !ShouldLimitLines(normalizedOutput))
        {
            return normalizedOutput;
        }

        var lineLimitedOutput = LimitLineWindow(normalizedOutput, DefaultMaxPreviewLines);
        var metadata = BuildMetadata(output, normalizedOutput, hasLongLine, lineLimitedOutput != normalizedOutput);
        if (metadata.Length + lineLimitedOutput.Length <= maxLength)
        {
            return metadata + lineLimitedOutput;
        }

        var available = maxLength - metadata.Length - OmittedMarker.Length;
        if (available <= 0)
        {
            return normalizedOutput[..Math.Min(normalizedOutput.Length, maxLength)];
        }

        var requestedTotal = normalizedHeadLimit + normalizedTailLimit;
        var headShare = requestedTotal == 0
            ? available / 2
            : available * normalizedHeadLimit / requestedTotal;
        var headLength = Math.Clamp(headShare, 1, available - 1);
        var tailLength = available - headLength;

        return metadata +
               lineLimitedOutput[..Math.Min(headLength, lineLimitedOutput.Length)] +
               OmittedMarker +
               lineLimitedOutput[^Math.Min(tailLength, lineLimitedOutput.Length)..];
    }

    private static string NormalizeLines(string output)
    {
        var lines = output.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            lines[index] = LimitLine(lines[index]);
        }

        return string.Join('\n', lines);
    }

    private static string LimitLine(string line)
    {
        if (line.Length <= DefaultMaxLineLength)
        {
            return line;
        }

        var available = DefaultMaxLineLength - LongLineMarker.Length;
        if (available <= 0)
        {
            return line[..DefaultMaxLineLength];
        }

        var headLength = Math.Max(1, available / 2);
        var tailLength = Math.Max(1, available - headLength);
        return line[..headLength] + LongLineMarker + line[^tailLength..];
    }

    private static bool ShouldLimitLines(string output)
    {
        return CountLines(output) > DefaultMaxPreviewLines;
    }

    private static string LimitLineWindow(string output, int maxLines)
    {
        var lines = output.Split('\n');
        if (lines.Length <= maxLines)
        {
            return output;
        }

        var headCount = Math.Max(1, maxLines / 2);
        var tailCount = Math.Max(1, maxLines - headCount);
        var head = lines.Take(headCount);
        var tail = lines.TakeLast(tailCount);

        return string.Join('\n', head) +
               OmittedMarker +
               string.Join('\n', tail);
    }

    private static string BuildMetadata(
        string originalOutput,
        string normalizedOutput,
        bool hasLongLine,
        bool hasLineWindow)
    {
        var metadata = $"Total terminal output chars: {originalOutput.Length}\n" +
                       $"Total terminal output lines: {CountLines(originalOutput)}\n";

        if (hasLongLine)
        {
            metadata += $"Preview line char budget: {DefaultMaxLineLength}\n";
        }

        if (hasLineWindow)
        {
            metadata += $"Preview line window: {DefaultMaxPreviewLines}\n";
        }

        if (normalizedOutput.Length != originalOutput.Length)
        {
            metadata += $"Preview normalized chars: {normalizedOutput.Length}\n";
        }

        return metadata + "\n";
    }

    private static int CountLines(string output)
    {
        return output.Count(character => character == '\n') + 1;
    }
}
