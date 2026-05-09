using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Logging;

namespace DevNexus.Core.Services;

/// <summary>
/// 发布中心管理服务实现。
/// </summary>
public class UpdateReleaseManagementService : IUpdateReleaseManagementService
{
    private readonly IUpdateReleaseRepository _releaseRepository;
    private readonly IUpdateRolloutManagementService _rolloutManagementService;
    private readonly ILogger<UpdateReleaseManagementService> _logger;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public UpdateReleaseManagementService(
        IUpdateReleaseRepository releaseRepository,
        IUpdateRolloutManagementService rolloutManagementService,
        ILogger<UpdateReleaseManagementService> logger)
    {
        _releaseRepository = releaseRepository;
        _rolloutManagementService = rolloutManagementService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReleaseDto>> GetReleasesAsync(CancellationToken cancellationToken = default)
    {
        var releases = await _releaseRepository.GetAllAsync(cancellationToken);
        return releases.Select(MapRelease).ToList();
    }

    /// <inheritdoc />
    public async Task<ReleaseDto> SaveReleaseAsync(SaveReleaseRequest request, CancellationToken cancellationToken = default)
    {
        ValidateReleaseRequest(request);

        var releaseId = request.ReleaseId ?? Guid.Empty;
        var release = releaseId == Guid.Empty
            ? new UpdateRelease { Id = Guid.Empty }
            : await _releaseRepository.GetByIdAsync(releaseId, cancellationToken) ?? new UpdateRelease { Id = releaseId };

        release.Version = request.Version.Trim();
        release.Channel = NormalizeOrDefault(request.Channel, "stable");
        release.Title = string.IsNullOrWhiteSpace(request.Title) ? $"DevNexus {release.Version}" : request.Title.Trim();
        release.ReleaseNotes = request.ReleaseNotes?.Trim() ?? string.Empty;
        release.Status = NormalizeReleaseStatus(request.Status);
        release.PublishedAt = release.Status == UpdateReleaseStatus.Published
            ? release.PublishedAt ?? DateTime.UtcNow
            : release.Status == UpdateReleaseStatus.Archived
                ? release.PublishedAt
                : null;

        release = await _releaseRepository.SaveAsync(release, cancellationToken);

        var artifacts = request.Artifacts.Select(artifact => new UpdateReleaseArtifact
        {
            Id = artifact.ArtifactId ?? Guid.Empty,
            ReleaseId = release.Id,
            Platform = NormalizeOrDefault(artifact.Platform, "desktop"),
            Architecture = NormalizeOrDefault(artifact.Architecture, "any"),
            PackageType = NormalizeOrDefault(artifact.PackageType, "installer"),
            FileName = artifact.FileName?.Trim() ?? string.Empty,
            FileSize = artifact.FileSize,
            Checksum = NormalizeNullable(artifact.Checksum),
            Signature = NormalizeNullable(artifact.Signature),
            DownloadUrl = artifact.DownloadUrl?.Trim() ?? string.Empty,
            StorageKey = NormalizeNullable(artifact.StorageKey)
        }).ToList();

        await _releaseRepository.ReplaceArtifactsAsync(release.Id, artifacts, cancellationToken);

        var refreshed = await _releaseRepository.GetByIdAsync(release.Id, cancellationToken)
            ?? throw new InvalidOperationException("保存后的发布版本不存在");

        _logger.LogInformation(
            "[UpdateReleaseManagementService] 已保存发布版本 | ReleaseId={ReleaseId} Version={Version} Status={Status}",
            refreshed.Id,
            refreshed.Version,
            refreshed.Status);

        return MapRelease(refreshed);
    }

    /// <inheritdoc />
    public async Task<ReleaseDto> PublishReleaseAsync(Guid releaseId, CancellationToken cancellationToken = default)
    {
        var release = await _releaseRepository.GetByIdAsync(releaseId, cancellationToken)
            ?? throw new InvalidOperationException($"发布版本 {releaseId} 不存在");

        release.Status = UpdateReleaseStatus.Published;
        release.PublishedAt = DateTime.UtcNow;

        release = await _releaseRepository.SaveAsync(release, cancellationToken);
        return MapRelease(release);
    }

    /// <inheritdoc />
    public async Task<ReleaseDto> ArchiveReleaseAsync(Guid releaseId, CancellationToken cancellationToken = default)
    {
        var release = await _releaseRepository.GetByIdAsync(releaseId, cancellationToken)
            ?? throw new InvalidOperationException($"发布版本 {releaseId} 不存在");

        release.Status = UpdateReleaseStatus.Archived;
        release = await _releaseRepository.SaveAsync(release, cancellationToken);
        return MapRelease(release);
    }

    /// <inheritdoc />
    public async Task DeleteReleaseAsync(Guid releaseId, CancellationToken cancellationToken = default)
    {
        var release = await _releaseRepository.GetByIdAsync(releaseId, cancellationToken)
            ?? throw new InvalidOperationException($"发布版本 {releaseId} 不存在");

        if (await _rolloutManagementService.GetRolloutsAsync(cancellationToken).ConfigureAwait(false) is { Count: > 0 } rollouts &&
            rollouts.Any(item => item.ReleaseId == releaseId))
        {
            throw new InvalidOperationException("当前版本仍被投放规则引用，先删除相关投放后再删除版本。");
        }

        await _releaseRepository.DeleteAsync(release, cancellationToken);

        _logger.LogInformation(
            "[UpdateReleaseManagementService] 已删除发布版本 | ReleaseId={ReleaseId} Version={Version}",
            release.Id,
            release.Version);
    }

    /// <inheritdoc />
    public async Task<ImportReleaseMetadataResult> ImportMetadataAsync(
        ImportReleaseMetadataRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Artifacts.Count == 0)
        {
            throw new InvalidOperationException("Artifacts 不能为空");
        }

        var release = await SaveReleaseAsync(new SaveReleaseRequest
        {
            Version = request.Version,
            Channel = request.Channel,
            Title = request.Title,
            ReleaseNotes = request.ReleaseNotes,
            Status = request.PublishRelease
                ? UpdateReleaseStatus.Published.ToWireValue()
                : UpdateReleaseStatus.Draft.ToWireValue(),
            Artifacts = request.Artifacts.Select(artifact => new SaveReleaseArtifactRequest
            {
                Platform = artifact.Platform,
                Architecture = artifact.Architecture,
                PackageType = artifact.PackageType,
                FileName = artifact.FileName,
                FileSize = artifact.FileSize,
                Checksum = artifact.Checksum,
                Signature = artifact.Signature,
                DownloadUrl = artifact.DownloadUrl,
                StorageKey = artifact.StorageKey
            }).ToList()
        }, cancellationToken);

        if (request.PublishRelease && release.Status != UpdateReleaseStatus.Published.ToWireValue())
        {
            release = await PublishReleaseAsync(release.ReleaseId, cancellationToken);
        }

        RolloutDto? rollout = null;
        if (request.CreateRollout && request.RolloutTemplate != null)
        {
            rollout = await _rolloutManagementService.SaveRolloutAsync(new SaveRolloutRequest
            {
                ReleaseId = release.ReleaseId,
                Platform = request.RolloutTemplate.Platform,
                Architecture = request.RolloutTemplate.Architecture,
                Channel = string.IsNullOrWhiteSpace(request.RolloutTemplate.Channel)
                    ? release.Channel
                    : request.RolloutTemplate.Channel,
                MinimumSupportedVersion = string.IsNullOrWhiteSpace(request.RolloutTemplate.MinimumSupportedVersion)
                    ? release.Version
                    : request.RolloutTemplate.MinimumSupportedVersion,
                RolloutPercent = request.RolloutTemplate.RolloutPercent,
                AudienceRule = request.RolloutTemplate.AudienceRule,
                ForceUpdate = request.RolloutTemplate.ForceUpdate,
                Enabled = request.RolloutTemplate.Enabled,
                StartsAt = DateTime.UtcNow
            }, cancellationToken);
        }

        return new ImportReleaseMetadataResult
        {
            Release = release,
            Rollout = rollout,
            Published = request.PublishRelease
        };
    }

    private static void ValidateReleaseRequest(SaveReleaseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Version))
        {
            throw new InvalidOperationException("Version 不能为空");
        }

        if (request.Artifacts.Any(artifact =>
                !string.Equals(NormalizeOrDefault(artifact.Platform, "desktop"), "web", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(artifact.DownloadUrl)))
        {
            throw new InvalidOperationException("非 Web 发布物必须提供 DownloadUrl");
        }
    }

    private static ReleaseDto MapRelease(UpdateRelease release)
    {
        return new ReleaseDto
        {
            ReleaseId = release.Id,
            Version = release.Version,
            Channel = release.Channel,
            Title = release.Title,
            ReleaseNotes = release.ReleaseNotes,
            PublishedAt = release.PublishedAt,
            Status = release.Status.ToWireValue(),
            CreatedAt = release.CreatedAt,
            UpdatedAt = release.UpdatedAt,
            Artifacts = release.Artifacts
                .OrderBy(artifact => artifact.Platform)
                .ThenBy(artifact => artifact.Architecture)
                .ThenBy(artifact => artifact.PackageType)
                .Select(artifact => new ReleaseArtifactDto
                {
                    ArtifactId = artifact.Id,
                    ReleaseId = artifact.ReleaseId,
                    Platform = artifact.Platform,
                    Architecture = artifact.Architecture,
                    PackageType = artifact.PackageType,
                    FileName = artifact.FileName,
                    FileSize = artifact.FileSize,
                    Checksum = artifact.Checksum,
                    Signature = artifact.Signature,
                    DownloadUrl = artifact.DownloadUrl,
                    StorageKey = artifact.StorageKey
                })
                .ToList()
        };
    }

    private static UpdateReleaseStatus NormalizeReleaseStatus(string? status)
    {
        return UpdateReleaseStatusExtensions.Parse(status);
    }

    private static string NormalizeOrDefault(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
