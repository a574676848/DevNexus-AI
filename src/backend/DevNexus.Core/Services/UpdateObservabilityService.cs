using DevNexus.Domain.Abstractions;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services;

/// <summary>
/// 更新观测摘要服务实现。
/// </summary>
public class UpdateObservabilityService : IUpdateObservabilityService
{
    private readonly IUpdateReleaseRepository _releaseRepository;
    private readonly IUpdateRolloutRepository _rolloutRepository;
    private readonly IUpdateClientEventRepository _eventRepository;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public UpdateObservabilityService(
        IUpdateReleaseRepository releaseRepository,
        IUpdateRolloutRepository rolloutRepository,
        IUpdateClientEventRepository eventRepository)
    {
        _releaseRepository = releaseRepository;
        _rolloutRepository = rolloutRepository;
        _eventRepository = eventRepository;
    }

    /// <inheritdoc />
    public async Task<UpdateObservabilitySummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var releases = await _releaseRepository.GetAllAsync(cancellationToken);
        var rollouts = await _rolloutRepository.GetAllAsync(cancellationToken);
        var events = await _eventRepository.GetSinceAsync(DateTime.UtcNow.AddDays(-30), cancellationToken);

        return new UpdateObservabilitySummaryDto
        {
            DraftReleaseCount = releases.Count(release => release.Status == UpdateReleaseStatus.Draft),
            PublishedReleaseCount = releases.Count(release => release.Status == UpdateReleaseStatus.Published),
            ArchivedReleaseCount = releases.Count(release => release.Status == UpdateReleaseStatus.Archived),
            ActiveRolloutCount = rollouts.Count(rollout => rollout.Enabled && !rollout.KillSwitchEnabled),
            PausedRolloutCount = rollouts.Count(rollout => !rollout.Enabled),
            MandatoryRolloutCount = rollouts.Count(rollout => rollout.ForceUpdate),
            KillSwitchCount = rollouts.Count(rollout => rollout.KillSwitchEnabled),
            ArtifactCount = releases.Sum(release => release.Artifacts.Count),
            CheckCount = events.Count(item => item.EventType == UpdateClientEventType.Check),
            UpdateAvailableCount = events.Count(item => item.EventType == UpdateClientEventType.UpdateAvailable),
            DownloadStartedCount = events.Count(item => item.EventType == UpdateClientEventType.DownloadStarted),
            DownloadCompletedCount = events.Count(item => item.EventType == UpdateClientEventType.DownloadCompleted),
            InstallCompletedCount = events.Count(item => item.EventType == UpdateClientEventType.InstallCompleted),
            FailedCount = events.Count(item => item.Result == UpdateClientEventResult.Failed)
        };
    }

    /// <inheritdoc />
    public async Task<UpdateObservabilityDetailDto> GetDetailsAsync(
        UpdateObservabilityFilterRequest? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new UpdateObservabilityFilterRequest();
        var days = Math.Clamp(filter.Days, 1, 180);
        var filterEventType = string.IsNullOrWhiteSpace(filter.EventType)
            ? (UpdateClientEventType?)null
            : UpdateClientEventTypeExtensions.Parse(filter.EventType);
        var filterResult = string.IsNullOrWhiteSpace(filter.Result)
            ? (UpdateClientEventResult?)null
            : UpdateClientEventResultExtensions.Parse(filter.Result);
        var events = await _eventRepository.GetSinceAsync(DateTime.UtcNow.AddDays(-days), cancellationToken);
        var filteredEvents = events
            .Where(item => !filter.ReleaseId.HasValue || item.ReleaseId == filter.ReleaseId)
            .Where(item => !filter.RolloutId.HasValue || item.RolloutId == filter.RolloutId)
            .Where(item => !filterEventType.HasValue || item.EventType == filterEventType.Value)
            .Where(item => !filterResult.HasValue || item.Result == filterResult.Value)
            .ToList();

        return new UpdateObservabilityDetailDto
        {
            RecentFailures = filteredEvents
                .Where(item => item.Result == UpdateClientEventResult.Failed)
                .Take(20)
                .Select(item => new UpdateClientEventDto
                {
                    InstallationId = item.InstallationId,
                    Platform = item.Platform,
                    Architecture = item.Architecture,
                    Channel = item.Channel,
                    CurrentVersion = item.CurrentVersion,
                    TargetVersion = item.TargetVersion,
                    RolloutId = item.RolloutId,
                    ReleaseId = item.ReleaseId,
                    ArtifactId = item.ArtifactId,
                    EventType = item.EventType.ToWireValue(),
                    Result = item.Result.ToWireValue(),
                    ErrorCode = item.ErrorCode,
                    ErrorMessage = item.ErrorMessage,
                    CreatedAt = item.CreatedAt
                })
                .ToList(),
            FailureReasons = filteredEvents
                .Where(item => item.Result == UpdateClientEventResult.Failed && !string.IsNullOrWhiteSpace(item.ErrorCode))
                .GroupBy(item => item.ErrorCode!)
                .Select(group => new UpdateFailureReasonDto
                {
                    ErrorCode = group.Key,
                    Count = group.Count()
                })
                .OrderByDescending(item => item.Count)
                .ToList(),
            EventMetrics = filteredEvents
                .GroupBy(item => new { item.EventType, item.Result })
                .Select(group => new UpdateEventMetricDto
                {
                    EventType = group.Key.EventType.ToWireValue(),
                    Result = group.Key.Result.ToWireValue(),
                    Count = group.Count()
                })
                .OrderByDescending(item => item.Count)
                .ToList(),
            DailyTrends = filteredEvents
                .GroupBy(item => item.CreatedAt.Date)
                .OrderBy(group => group.Key)
                .Select(group => new UpdateDailyTrendDto
                {
                    Date = group.Key,
                    CheckCount = group.Count(item => item.EventType == UpdateClientEventType.Check),
                    UpdateAvailableCount = group.Count(item => item.EventType == UpdateClientEventType.UpdateAvailable),
                    InstallCompletedCount = group.Count(item => item.EventType == UpdateClientEventType.InstallCompleted),
                    FailedCount = group.Count(item => item.Result == UpdateClientEventResult.Failed)
                })
                .ToList()
        };
    }
}
