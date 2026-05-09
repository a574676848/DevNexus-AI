using System.Collections.Concurrent;

namespace DevNexus.Client.Shared.Services.UI;

/// <summary>
/// 通知去重策略，避免短时间内重复弹出相同提示。
/// </summary>
public static class NotificationDeduplicationPolicy
{
    private static readonly ConcurrentDictionary<string, DateTime> LastShownAt = new();
    private static readonly TimeSpan DefaultSuppressWindow = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 判断当前通知是否应被抑制。
    /// </summary>
    public static bool ShouldSuppress(string title, string message, TimeSpan? suppressWindow = null, string? dedupeKey = null)
    {
        var normalizedTitle = string.IsNullOrWhiteSpace(title) ? string.Empty : title.Trim();
        var normalizedMessage = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
        var key = string.IsNullOrWhiteSpace(dedupeKey)
            ? $"{normalizedTitle}::{normalizedMessage}"
            : dedupeKey.Trim();
        var now = DateTime.UtcNow;
        var window = suppressWindow ?? DefaultSuppressWindow;

        if (LastShownAt.TryGetValue(key, out var lastShownAt) && now - lastShownAt < window)
        {
            return true;
        }

        LastShownAt[key] = now;
        return false;
    }
}
