namespace DevNexus.Core.Services.Chat;

/// <summary>
/// Agent Loop 停止信号策略。
/// </summary>
internal static class AgentLoopStopSignalPolicy
{
    /// <summary>
    /// Agent Loop 停止标记。
    /// </summary>
    public const string StopMarker = "[AGENT_LOOP_STOP]";

    /// <summary>
    /// 判断模型响应是否明确要求停止自动修复。
    /// </summary>
    public static bool ShouldStop(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return false;
        }

        var lines = response
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
        if (lines.Count == 0)
        {
            return false;
        }

        return string.Equals(lines[^1], StopMarker, StringComparison.Ordinal);
    }
}
