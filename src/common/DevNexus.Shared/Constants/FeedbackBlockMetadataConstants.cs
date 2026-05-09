using DevNexus.Shared.Enums;

namespace DevNexus.Shared.Constants;

/// <summary>
/// Warning、Thinking 等反馈类 Block metadata 的共享协议定义。
/// </summary>
public static class FeedbackBlockMetadataConstants
{
    public const string Level = "level";
    public const string Title = "title";
    public const string Source = "source";
    public const string Collapsed = "collapsed";
    public const string ToolStatus = "toolStatus";

    public const string LevelWarning = "warning";
    public const string LevelInfo = "info";
    public const string LevelError = "error";

    public const string SourcePlugin = "Plugin";
    public const string SourceToolInvocation = "ToolInvocation";
    public const string SourceSwarmCoordinator = "SwarmCoordinator";
    public const string SourceChatServiceSwarm = "ChatService.Swarm";

    /// <summary>
    /// 规范化反馈级别。
    /// </summary>
    public static string NormalizeLevel(string? level, string fallback = LevelWarning)
    {
        return level?.Trim().ToLowerInvariant() switch
        {
            LevelInfo => LevelInfo,
            LevelError => LevelError,
            LevelWarning => LevelWarning,
            _ => fallback
        };
    }

    /// <summary>
    /// 规范化反馈来源；未知值保留原值，空值回退到默认来源。
    /// </summary>
    public static string NormalizeSource(string? source, string fallback = SourcePlugin)
    {
        return string.IsNullOrWhiteSpace(source) ? fallback : source.Trim();
    }

    /// <summary>
    /// 规范化工具调用状态协议值。
    /// </summary>
    public static string NormalizeToolStatus(string? status)
    {
        return ToolInvocationStatusExtensions.Parse(status).ToWireValue();
    }
}
