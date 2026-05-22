using System.Text;
using System.Text.RegularExpressions;

namespace DevNexus.Core.Services.Terminal;

/// <summary>
/// 终端输出文本清洗器。
/// </summary>
public static class TerminalOutputTextSanitizer
{
    private static readonly Regex AnsiRegex = new(
        @"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])",
        RegexOptions.Compiled);

    private static readonly Regex[] WaitingInputPatterns =
    [
        new Regex(@"(?i)password\s*[:：]?$", RegexOptions.Compiled),
        new Regex(@"(?i)continue\?\s*\[y/n\]", RegexOptions.Compiled),
        new Regex(@"(?i)press\s+enter\s+to\s+continue", RegexOptions.Compiled),
        new Regex(@"(?i)confirm\s*\[y/n\]", RegexOptions.Compiled),
        new Regex(@"(?i)enter\s+.*[:：]$", RegexOptions.Compiled)
    ];

    /// <summary>
    /// 清理终端输出，移除 ANSI 序列和非文本控制字符。
    /// </summary>
    public static string Clean(string text)
    {
        return ScrubControlCharacters(StripAnsi(text));
    }

    /// <summary>
    /// 移除终端 ANSI 控制序列。
    /// </summary>
    public static string StripAnsi(string text)
    {
        return string.IsNullOrEmpty(text)
            ? string.Empty
            : AnsiRegex.Replace(text, string.Empty);
    }

    /// <summary>
    /// 移除不适合进入会话历史、JSON 载荷或 UI 渲染的控制字符。
    /// </summary>
    public static string ScrubControlCharacters(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        StringBuilder? builder = null;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (IsAllowedCharacter(character))
            {
                builder?.Append(character);
                continue;
            }

            builder ??= new StringBuilder(text.Length).Append(text, 0, index);
        }

        return builder?.ToString() ?? text;
    }

    /// <summary>
    /// 判断输出是否正在等待用户输入。
    /// </summary>
    public static bool IsWaitingForInput(string text)
    {
        var normalized = Clean(text).Trim();
        return WaitingInputPatterns.Any(pattern => pattern.IsMatch(normalized));
    }

    private static bool IsAllowedCharacter(char character)
    {
        return character is '\r' or '\n' or '\t' || !char.IsControl(character);
    }
}
