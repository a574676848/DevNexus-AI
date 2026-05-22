using DevNexus.Core.Services.Terminal;

namespace DevNexus.Infrastructure.Services.CliTerminal;

/// <summary>
/// CLI 输出文本清理工具。
/// </summary>
internal static class CliOutputTextSanitizer
{
    /// <summary>
    /// 移除终端 ANSI 控制序列。
    /// </summary>
    public static string StripAnsi(string text)
    {
        return TerminalOutputTextSanitizer.Clean(text);
    }

    /// <summary>
    /// 判断输出是否正在等待用户输入。
    /// </summary>
    public static bool IsWaitingForInput(string text)
    {
        return TerminalOutputTextSanitizer.IsWaitingForInput(text);
    }
}
