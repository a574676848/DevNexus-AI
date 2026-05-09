using System.Security.Cryptography;
using System.Text;
using DevNexus.Domain.Abstractions;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Logging;

namespace DevNexus.Core.Services;

/// <summary>
/// 更新 Manifest 决策服务实现。
/// </summary>
public class UpdateManifestService : IUpdateManifestService
{
    private readonly IUpdateRolloutRepository _rolloutRepository;
    private readonly ILogger<UpdateManifestService> _logger;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public UpdateManifestService(
        IUpdateRolloutRepository rolloutRepository,
        ILogger<UpdateManifestService> logger)
    {
        _rolloutRepository = rolloutRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UpdateManifestResponse> GetManifestAsync(
        UpdateManifestRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedPlatform = NormalizeOrDefault(request.Platform, "desktop");
        var normalizedArchitecture = NormalizeOrDefault(request.Architecture, "any");
        var normalizedChannel = NormalizeOrDefault(request.Channel, "stable");
        var utcNow = DateTime.UtcNow;

        var candidates = await _rolloutRepository.GetManifestCandidatesAsync(
            normalizedPlatform,
            normalizedArchitecture,
            normalizedChannel,
            utcNow,
            cancellationToken);

        var matchedRollout = candidates.FirstOrDefault(rollout =>
            rollout.Release?.Status == UpdateReleaseStatus.Published &&
            IsAudienceMatched(rollout.AudienceRule, request) &&
            IsInRolloutPercent(rollout.RolloutPercent, request));

        if (matchedRollout?.Release == null)
        {
            return CreateEmptyManifest(request, normalizedPlatform, normalizedArchitecture, normalizedChannel, "no-matching-rollout");
        }

        var release = matchedRollout.Release;
        var decision = CalculateDecision(request.CurrentVersion, matchedRollout.MinimumSupportedVersion, release.Version, matchedRollout.ForceUpdate);
        if (decision == UpdateDecision.None)
        {
            return CreateEmptyManifest(request, normalizedPlatform, normalizedArchitecture, normalizedChannel, "up-to-date");
        }

        var artifacts = release.Artifacts
            .Where(artifact => string.Equals(artifact.Platform, normalizedPlatform, StringComparison.OrdinalIgnoreCase))
            .Where(artifact => string.Equals(artifact.Architecture, normalizedArchitecture, StringComparison.OrdinalIgnoreCase)
                               || string.Equals(artifact.Architecture, "any", StringComparison.OrdinalIgnoreCase)
                               || string.Equals(artifact.Architecture, "*", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(artifact => string.Equals(artifact.Architecture, normalizedArchitecture, StringComparison.OrdinalIgnoreCase))
            .ThenBy(artifact => artifact.PackageType)
            .Select(artifact => new UpdateArtifactDto
            {
                ArtifactId = artifact.Id,
                ReleaseId = release.Id,
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
            .ToList();

        var manifest = new UpdateManifestResponse
        {
            ManifestVersion = "2.0",
            ClientPlatform = normalizedPlatform,
            Architecture = normalizedArchitecture,
            Channel = normalizedChannel,
            CurrentVersion = request.CurrentVersion ?? string.Empty,
            Decision = decision,
            Mandatory = decision == UpdateDecision.Required,
            TargetRelease = new UpdateReleaseDto
            {
                ReleaseId = release.Id,
                Version = release.Version,
                Channel = release.Channel,
                Title = release.Title,
                ReleaseNotes = release.ReleaseNotes,
                PublishedAt = release.PublishedAt ?? release.CreatedAt,
                Status = release.Status.ToWireValue()
            },
            Artifacts = artifacts,
            Reason = BuildReason(decision, request.CurrentVersion, matchedRollout.MinimumSupportedVersion),
            RolloutId = matchedRollout.Id,
            ServerTime = DateTimeOffset.UtcNow
        };

        _logger.LogInformation(
            "[UpdateManifestService] 生成 Manifest | Platform={Platform} Version={CurrentVersion} Decision={Decision} RolloutId={RolloutId}",
            normalizedPlatform,
            request.CurrentVersion,
            manifest.Decision,
            matchedRollout.Id);

        return manifest;
    }

    private static UpdateManifestResponse CreateEmptyManifest(
        UpdateManifestRequest request,
        string platform,
        string architecture,
        string channel,
        string reason)
    {
        return new UpdateManifestResponse
        {
            ManifestVersion = "2.0",
            ClientPlatform = platform,
            Architecture = architecture,
            Channel = channel,
            CurrentVersion = request.CurrentVersion ?? string.Empty,
            Decision = UpdateDecision.None,
            Mandatory = false,
            Reason = reason,
            ServerTime = DateTimeOffset.UtcNow
        };
    }

    private static UpdateDecision CalculateDecision(string? currentVersion, string minimumVersion, string targetVersion, bool forceUpdate)
    {
        var current = ParseVersionOrDefault(currentVersion, new Version(0, 0, 0, 0));
        var minimum = ParseVersionOrDefault(minimumVersion, new Version(0, 0, 0, 0));
        var target = ParseVersionOrDefault(targetVersion, minimum);

        if (current < minimum)
        {
            return UpdateDecision.Required;
        }

        if (current < target)
        {
            return forceUpdate ? UpdateDecision.Required : UpdateDecision.Recommended;
        }

        return UpdateDecision.None;
    }

    private static string BuildReason(UpdateDecision decision, string? currentVersion, string minimumVersion)
    {
        var current = ParseVersionOrDefault(currentVersion, new Version(0, 0, 0, 0));
        var minimum = ParseVersionOrDefault(minimumVersion, new Version(0, 0, 0, 0));

        return decision switch
        {
            UpdateDecision.Required when current < minimum => "below-minimum-supported-version",
            UpdateDecision.Required => "mandatory-rollout-hit",
            UpdateDecision.Recommended => "newer-release-available",
            _ => "up-to-date"
        };
    }

    private static bool IsAudienceMatched(string? audienceRule, UpdateManifestRequest request)
    {
        if (string.IsNullOrWhiteSpace(audienceRule) || string.Equals(audienceRule, "all", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var clauses = audienceRule.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var clause in clauses)
        {
            var parts = clause.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                continue;
            }

            var key = parts[0].ToLowerInvariant();
            var value = parts[1];
            var matched = key switch
            {
                "tenant" => string.Equals(request.TenantId, value, StringComparison.OrdinalIgnoreCase),
                "user" => string.Equals(request.UserIdHash, value, StringComparison.OrdinalIgnoreCase),
                "installation" => string.Equals(request.InstallationId, value, StringComparison.OrdinalIgnoreCase),
                "capability" => request.ClientCapabilities.Any(capability => string.Equals(capability, value, StringComparison.OrdinalIgnoreCase)),
                _ => true
            };

            if (!matched)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsInRolloutPercent(int rolloutPercent, UpdateManifestRequest request)
    {
        if (rolloutPercent >= 100)
        {
            return true;
        }

        if (rolloutPercent <= 0)
        {
            return false;
        }

        var seed = request.InstallationId ?? request.UserIdHash ?? request.TenantId;
        if (string.IsNullOrWhiteSpace(seed))
        {
            return false;
        }

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(seed));
        var bucket = BitConverter.ToUInt32(hash, 0) % 100;
        return bucket < rolloutPercent;
    }

    private static Version ParseVersionOrDefault(string? value, Version fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        var parts = normalized.Split('.');
        normalized = parts.Length switch
        {
            1 => $"{parts[0]}.0.0.0",
            2 => $"{parts[0]}.{parts[1]}.0.0",
            3 => $"{parts[0]}.{parts[1]}.{parts[2]}.0",
            _ => normalized
        };

        return Version.TryParse(normalized, out var parsed) ? parsed : fallback;
    }

    private static string NormalizeOrDefault(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();
    }
}
