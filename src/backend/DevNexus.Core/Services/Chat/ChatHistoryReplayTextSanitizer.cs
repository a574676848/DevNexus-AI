using System.Text;
using System.Text.RegularExpressions;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 聊天历史回放文本清洗器。
/// </summary>
public static class ChatHistoryReplayTextSanitizer
{
    private static readonly Regex AnsiRegex = new(
        @"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])",
        RegexOptions.Compiled);

    /// <summary>
    /// 清理进入模型历史回放的文本，避免终端控制序列或不可见控制字符污染 Prompt。
    /// </summary>
    public static string Clean(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var withoutAnsi = AnsiRegex.Replace(text, string.Empty);
        return ScrubControlCharacters(withoutAnsi);
    }

    private static string ScrubControlCharacters(string text)
    {
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

    private static bool IsAllowedCharacter(char character)
    {
        return character is '\r' or '\n' or '\t' || !char.IsControl(character);
    }
}
