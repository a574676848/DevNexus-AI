namespace DevNexus.Core.Services.Cli;

/// <summary>
/// CLI 命令完成哨兵协议。
/// </summary>
public static class CliCommandCompletionProtocol
{
    private const string SentinelPrefix = "__DEVNEXUS_CLI_EXIT_";
    private const string SentinelSuffix = "__";

    /// <summary>
    /// 创建命令完成哨兵。
    /// </summary>
    public static string CreateSentinel()
    {
        return $"{SentinelPrefix}{Guid.NewGuid():N}{SentinelSuffix}";
    }

    /// <summary>
    /// 将原始命令包装为可回收退出码的命令。
    /// </summary>
    public static string BuildCommand(string command, string sentinel, bool isWindows)
    {
        return isWindows
            ? $"{command}; $devnexusExitCode = if ($null -ne $LASTEXITCODE) {{ $LASTEXITCODE }} elseif ($?) {{ 0 }} else {{ 1 }}; echo '{sentinel}'; echo $devnexusExitCode"
            : $"{command}; devnexus_exit_code=$?; echo '{sentinel}'; echo $devnexus_exit_code";
    }

    /// <summary>
    /// 尝试从输出片段中解析命令完成结果。
    /// </summary>
    public static bool TryParseCompletion(
        string output,
        string sentinel,
        out CliCommandCompletionResult result)
    {
        result = CliCommandCompletionResult.Incomplete;
        if (string.IsNullOrEmpty(output) || string.IsNullOrWhiteSpace(sentinel))
        {
            return false;
        }

        var sentinelIndex = output.IndexOf(sentinel, StringComparison.Ordinal);
        if (sentinelIndex < 0)
        {
            return false;
        }

        var beforeSentinel = output[..sentinelIndex].TrimEnd();
        var afterSentinel = output[(sentinelIndex + sentinel.Length)..];
        var exitCode = ParseExitCode(afterSentinel);
        result = new CliCommandCompletionResult(true, beforeSentinel, exitCode);
        return true;
    }

    private static int ParseExitCode(string text)
    {
        var lines = text.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length > 0 && int.TryParse(lines[0], out var exitCode)
            ? exitCode
            : -1;
    }
}

/// <summary>
/// CLI 命令完成解析结果。
/// </summary>
public sealed record CliCommandCompletionResult(
    bool IsCompleted,
    string CleanOutput,
    int ExitCode)
{
    /// <summary>
    /// 未完成结果。
    /// </summary>
    public static CliCommandCompletionResult Incomplete { get; } = new(false, string.Empty, -1);
}
