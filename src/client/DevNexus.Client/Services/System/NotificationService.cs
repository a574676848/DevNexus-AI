using DevNexus.Client.Shared.Services.UI;

namespace DevNexus.Client.Services.System;

/// <summary>
/// MAUI 跨平台通知服务实现
/// </summary>
public class NotificationService : INotificationService
{
    private int _notificationIdCounter = 0;

    /// <inheritdoc />
    public async Task ShowAsync(string title, string message, string? iconPath = null)
    {
#if WINDOWS
        // Windows 使用 ToastNotification
        await ShowWindowsNotificationAsync(title, message, iconPath);
#else
        // 其他平台使用 MAUI 的原生消息通道 (DisplayAlert)
        // 必须在主线程执行
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (Application.Current?.MainPage != null)
            {
                await Application.Current.MainPage.DisplayAlert(title, message, "确定");
            }
        });
        await Task.CompletedTask;
#endif
    }

    /// <inheritdoc />
    public async Task ShowDeduplicatedAsync(
        string title,
        string message,
        string? iconPath = null,
        int suppressSeconds = 5,
        string? dedupeKey = null)
    {
        if (NotificationDeduplicationPolicy.ShouldSuppress(
                title,
                message,
                TimeSpan.FromSeconds(suppressSeconds),
                dedupeKey))
        {
            return;
        }

        await ShowAsync(title, message, iconPath);
    }

    /// <inheritdoc />
    public Task<int> ScheduleAsync(string title, string message, DateTime scheduledTime)
    {
        var id = Interlocked.Increment(ref _notificationIdCounter);
        // TODO: 实现定时通知
        return Task.FromResult(id);
    }

    /// <inheritdoc />
    public Task CancelAsync(int notificationId)
    {
        // TODO: 取消定时通知
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> CheckPermissionAsync()
    {
        // Windows 默认有权限，其他平台需要检查
#if WINDOWS
        return Task.FromResult(true);
#else
        // 简化处理，假设都有权限
        return Task.FromResult(true);
#endif
    }

    /// <inheritdoc />
    public Task<bool> RequestPermissionAsync()
    {
        // Windows 不需要请求权限，其他平台可能需要
        return Task.FromResult(true);
    }

#if WINDOWS
    private Task ShowWindowsNotificationAsync(string title, string message, string? iconPath)
    {
        try
        {
            // 使用 Windows ToastNotification
            var xml = $@"
                <toast>
                    <visual>
                        <binding template='ToastGeneric'>
                            <text>{EscapeXml(title)}</text>
                            <text>{EscapeXml(message)}</text>
                        </binding>
                    </visual>
                </toast>";

            var doc = new Windows.Data.Xml.Dom.XmlDocument();
            doc.LoadXml(xml);

            var notification = new Windows.UI.Notifications.ToastNotification(doc);
            Windows.UI.Notifications.ToastNotificationManager
                .CreateToastNotifier("DevNexus.Client")
                .Show(notification);
        }
        catch (Exception ex)
        {
            global::System.Diagnostics.Debug.WriteLine($"[NotificationService] Failed to show notification: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private static string EscapeXml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
#endif
}

