namespace DevNexus.Client.Shared.Components.Terminal;

/// <summary>
/// 终端实时输出视口策略，避免长任务把完整 scrollback 直接压进浏览器渲染树。
/// </summary>
public sealed class TerminalLiveOutputViewportPolicy
{
    public const int DefaultMaxVisibleChars = 32_000;

    public static TerminalLiveOutputViewportPolicy Default { get; } = new();

    public TerminalLiveOutputViewportPolicy(int maxVisibleChars = DefaultMaxVisibleChars)
    {
        MaxVisibleChars = Math.Max(1, maxVisibleChars);
    }

    public int MaxVisibleChars { get; }

    public TerminalLiveOutputViewport Create(string? output)
    {
        if (string.IsNullOrEmpty(output))
        {
            return new TerminalLiveOutputViewport(string.Empty, false, 0, 0, 0);
        }

        if (output.Length <= MaxVisibleChars)
        {
            return new TerminalLiveOutputViewport(output, false, 0, 0, CountLines(output));
        }

        var startIndex = Math.Max(0, output.Length - MaxVisibleChars);
        var lineBreakIndex = output.IndexOf('\n', startIndex);
        if (lineBreakIndex >= 0 && lineBreakIndex < output.Length - 1)
        {
            startIndex = lineBreakIndex + 1;
        }

        var text = output[startIndex..];
        return new TerminalLiveOutputViewport(
            text,
            true,
            startIndex,
            CountCompletedLines(output[..startIndex]),
            CountLines(text));
    }

    private static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return text.Count(character => character == '\n') + 1;
    }

    private static int CountCompletedLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return text.Count(character => character == '\n');
    }
}

public sealed record TerminalLiveOutputViewport(
    string Text,
    bool WasTrimmed,
    int HiddenCharCount,
    int HiddenLineCount,
    int VisibleLineCount);
