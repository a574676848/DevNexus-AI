using DevNexus.Shared.Constants;
using DevNexus.Shared.Enums;

namespace DevNexus.Client.Shared.Helpers;

public static class TerminalDisplayHelper
{
    public static string GetSessionToneClass(string? sessionState, bool waitingForInput = false)
    {
        if (waitingForInput)
        {
            return "terminal-summary-card--waiting";
        }

        return CliSessionStateExtensions.Parse(sessionState) switch
        {
            CliSessionState.Completed => "terminal-summary-card--success",
            CliSessionState.Failed => "terminal-summary-card--danger",
            CliSessionState.Cancelled => "terminal-summary-card--warning",
            CliSessionState.Running => "terminal-summary-card--active",
            _ => "terminal-summary-card--neutral"
        };
    }

    public static string FormatSessionState(string? sessionState)
    {
        return CliSessionStateExtensions.Parse(sessionState) switch
        {
            CliSessionState.Running => "运行中",
            CliSessionState.Completed => "已完成",
            CliSessionState.Failed => "失败",
            CliSessionState.Cancelled => "已停止",
            CliSessionState.Reaped => "已结束",
            CliSessionState.TimedOut => "已超时",
            _ => string.IsNullOrWhiteSpace(sessionState) ? "未知" : sessionState
        };
    }

    public static string FormatTerminationReason(string? terminationReason)
    {
        return CliSessionTerminationReasons.GetDisplayText(terminationReason);
    }

    public static string? FormatRelativeTime(DateTime? timestamp)
    {
        if (!timestamp.HasValue)
        {
            return null;
        }

        var local = timestamp.Value.Kind == DateTimeKind.Utc
            ? timestamp.Value.ToLocalTime()
            : timestamp.Value;

        var delta = DateTime.Now - local;

        if (delta < TimeSpan.FromMinutes(1))
        {
            return "刚刚";
        }

        if (delta < TimeSpan.FromHours(1))
        {
            return $"{Math.Max(1, delta.Minutes)} 分钟前";
        }

        if (delta < TimeSpan.FromDays(1))
        {
            return $"{Math.Max(1, delta.Hours)} 小时前";
        }

        if (delta < TimeSpan.FromDays(7))
        {
            return $"{Math.Max(1, delta.Days)} 天前";
        }

        if (local.Year == DateTime.Now.Year)
        {
            return local.ToString("M/d HH:mm");
        }

        return local.ToString("yyyy/M/d HH:mm");
    }

    public static string? FormatAbsoluteTime(DateTime? timestamp)
    {
        if (!timestamp.HasValue)
        {
            return null;
        }

        var local = timestamp.Value.Kind == DateTimeKind.Utc
            ? timestamp.Value.ToLocalTime()
            : timestamp.Value;

        return local.Year == DateTime.Now.Year
            ? local.ToString("M/d HH:mm")
            : local.ToString("yyyy/M/d HH:mm");
    }
}
