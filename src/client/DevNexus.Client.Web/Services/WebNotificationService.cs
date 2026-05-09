using Microsoft.JSInterop;
using DevNexus.Client.Shared.Services.UI;
namespace DevNexus.Client.Web.Services;

/// <summary>
/// Web 通知服务实现 - 基于 Web Notification API
/// </summary>
public class WebNotificationService : INotificationService
{
    private readonly IJSRuntime _js;

    public WebNotificationService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task ShowAsync(string title, string message, string? iconPath = null)
    {
        await _js.InvokeVoidAsync("eval", $@"
            if (Notification.permission === 'granted') {{
                new Notification('{EscapeJs(title)}', {{
                    body: '{EscapeJs(message)}',
                    icon: '{iconPath ?? "/icon-192.png"}'
                }});
            }}
        ");
    }

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

    public async Task<int> ScheduleAsync(string title, string message, DateTime scheduledTime)
    {
        // Web 端使用 setTimeout 定时通知
        var delayMs = (scheduledTime - DateTime.Now).TotalMilliseconds;
        if (delayMs <= 0) return 0;

        return await _js.InvokeAsync<int>("eval", $@"
            setTimeout(() => {{
                if (Notification.permission === 'granted') {{
                    new Notification('{EscapeJs(title)}', {{ body: '{EscapeJs(message)}' }});
                }}
            }}, {delayMs})
        ");
    }

    public async Task CancelAsync(int notificationId)
    {
        await _js.InvokeVoidAsync("eval", $"clearTimeout({notificationId})");
    }

    public async Task<bool> CheckPermissionAsync()
    {
        return await _js.InvokeAsync<bool>("eval", "Notification.permission === 'granted'");
    }

    public async Task<bool> RequestPermissionAsync()
    {
        var result = await _js.InvokeAsync<string>("eval", "Notification.requestPermission()");
        return result == "granted";
    }

    private string EscapeJs(string text)
    {
        return text.Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");
    }
}

