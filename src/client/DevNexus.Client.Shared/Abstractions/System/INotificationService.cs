namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// 本地通知服务接口
/// </summary>
public interface INotificationService
{
    Task ShowAsync(string title, string message, string? iconPath = null);
    Task ShowDeduplicatedAsync(
        string title,
        string message,
        string? iconPath = null,
        int suppressSeconds = 5,
        string? dedupeKey = null);
    Task<int> ScheduleAsync(string title, string message, DateTime scheduledTime);
    Task CancelAsync(int notificationId);
    Task<bool> CheckPermissionAsync();
    Task<bool> RequestPermissionAsync();
}

