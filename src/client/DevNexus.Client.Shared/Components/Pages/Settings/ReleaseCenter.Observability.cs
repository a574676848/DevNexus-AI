using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Client.Shared.Components.Pages.Settings;

/// <summary>
/// 版本发布中心的观测、统计与结果展示。
/// </summary>
public partial class ReleaseCenter
{
    private IReadOnlyList<MetricItem> GetObservabilityMetrics()
    {
        return
        [
            new MetricItem("草稿版本", observability.DraftReleaseCount),
            new MetricItem("已发布版本", observability.PublishedReleaseCount),
            new MetricItem("已归档版本", observability.ArchivedReleaseCount),
            new MetricItem("激活投放", observability.ActiveRolloutCount),
            new MetricItem("暂停投放", observability.PausedRolloutCount),
            new MetricItem("强制更新投放", observability.MandatoryRolloutCount),
            new MetricItem("熔断开关", observability.KillSwitchCount),
            new MetricItem("发布物总数", observability.ArtifactCount),
            new MetricItem("检查次数", observability.CheckCount),
            new MetricItem("命中新版本", observability.UpdateAvailableCount),
            new MetricItem("开始下载", observability.DownloadStartedCount),
            new MetricItem("下载完成", observability.DownloadCompletedCount),
            new MetricItem("安装完成", observability.InstallCompletedCount),
            new MetricItem("失败次数", observability.FailedCount)
        ];
    }

    private int ReadyReleaseCount => releases.Count(item => UpdateReleaseStatusExtensions.Parse(item.Status) == UpdateReleaseStatus.Published);
    private int ActiveRolloutCount => rollouts.Count(item => item.Enabled && !item.KillSwitchEnabled);
    private ReleaseDto? LatestRelease => releases.OrderByDescending(item => item.CreatedAt).FirstOrDefault();
    private RolloutDto? LatestRollout => rollouts.OrderByDescending(item => item.UpdatedAt).FirstOrDefault();

    internal string GetReleaseStatusText(string? status) => UpdateReleaseStatusExtensions.Parse(status) switch
    {
        UpdateReleaseStatus.Draft => "草稿",
        UpdateReleaseStatus.Published => "已发布",
        UpdateReleaseStatus.Archived => "已归档",
        _ => "未知状态"
    };

    internal string GetRolloutStatusText(RolloutDto rollout)
    {
        if (rollout.KillSwitchEnabled)
        {
            return "已熔断";
        }

        return rollout.Enabled ? "投放中" : "已暂停";
    }

    private string GetRolloutStatusClass(RolloutDto rollout)
    {
        if (rollout.KillSwitchEnabled)
        {
            return "status-chip--archived";
        }

        return rollout.Enabled ? "status-chip--published" : "status-chip--draft";
    }

    private string GetResultText(string? result) => UpdateClientEventResultExtensions.Parse(result) switch
    {
        UpdateClientEventResult.Success => "成功",
        UpdateClientEventResult.Failed => "失败",
        _ => "未知"
    };

    private string GetEventTypeText(string? eventType) => UpdateClientEventTypeExtensions.Parse(eventType) switch
    {
        UpdateClientEventType.Check => "检查更新",
        UpdateClientEventType.UpdateAvailable => "发现新版本",
        UpdateClientEventType.DownloadStarted => "开始下载",
        UpdateClientEventType.DownloadCompleted => "下载完成",
        UpdateClientEventType.VerifyCompleted => "校验完成",
        UpdateClientEventType.UpdaterLaunched => "启动安装器",
        UpdateClientEventType.InstallerOpened => "打开安装器",
        UpdateClientEventType.InstallCompleted => "安装完成",
        UpdateClientEventType.InstallFailed => "安装失败",
        _ => eventType ?? "未知事件"
    };

    private static string GetPreviewDecisionText(object? decision)
    {
        var text = decision?.ToString();
        return string.IsNullOrWhiteSpace(text) ? "未命中" : text;
    }

    private int GetReleaseRolloutCount(Guid releaseId)
    {
        return rollouts.Count(item => item.ReleaseId == releaseId);
    }

    private void ShowSuccessToast(string message)
    {
        ToastService.Success(message);
    }

    private void ShowErrorToast(string message)
    {
        ToastService.Error(message);
    }

    private void RefreshTrendPolylines()
    {
        checkTrendPolyline = BuildTrendPolyline(item => item.CheckCount);
        updateAvailableTrendPolyline = BuildTrendPolyline(item => item.UpdateAvailableCount);
        installCompletedTrendPolyline = BuildTrendPolyline(item => item.InstallCompletedCount);
        failedTrendPolyline = BuildTrendPolyline(item => item.FailedCount);
    }

    private string BuildTrendPolyline(Func<UpdateDailyTrendDto, int> selector)
    {
        if (observabilityDetails.DailyTrends.Count == 0)
        {
            return string.Empty;
        }

        var values = observabilityDetails.DailyTrends.Select(selector).ToList();
        var max = Math.Max(1, values.Max());
        var width = 100d;
        var height = 44d;
        if (values.Count == 1)
        {
            var y = Math.Round(height - ((double)values[0] / max * height), 2);
            return $"0,{y} 100,{y}";
        }

        var step = width / (values.Count - 1);
        return string.Join(" ", values.Select((value, index) =>
        {
            var x = Math.Round(index * step, 2);
            var y = Math.Round(height - ((double)value / max * height), 2);
            return $"{x},{y}";
        }));
    }

    private sealed record MetricItem(string Label, int Value);
}
