using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Components.Pages.Settings;

/// <summary>
/// 版本发布中心表单模型。
/// </summary>
public partial class ReleaseCenter
{
    /// <summary>
    /// 发布版本表单模型。
    /// </summary>
    public sealed class ReleaseFormModel
    {
        public Guid? ReleaseId { get; set; }
        public string Version { get; set; } = string.Empty;
        public string Channel { get; set; } = "stable";
        public string Title { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public List<ArtifactFormModel> Artifacts { get; set; } = new();

        public static ReleaseFormModel CreateEmpty()
        {
            return new ReleaseFormModel
            {
                Artifacts = new List<ArtifactFormModel> { new() }
            };
        }

        public static ReleaseFormModel FromDto(ReleaseDto dto)
        {
            return new ReleaseFormModel
            {
                ReleaseId = dto.ReleaseId,
                Version = dto.Version,
                Channel = dto.Channel,
                Title = dto.Title,
                ReleaseNotes = dto.ReleaseNotes,
                Artifacts = dto.Artifacts.Select(ArtifactFormModel.FromDto).ToList()
            };
        }

        public SaveReleaseRequest ToRequest()
        {
            return new SaveReleaseRequest
            {
                ReleaseId = ReleaseId,
                Version = Version.Trim(),
                Channel = Channel.Trim(),
                Title = Title.Trim(),
                ReleaseNotes = ReleaseNotes.Trim(),
                Artifacts = Artifacts.Select(item => item.ToRequest()).ToList()
            };
        }
    }

    /// <summary>
    /// 发布物表单模型。
    /// </summary>
    public sealed class ArtifactFormModel
    {
        public Guid? ArtifactId { get; set; }
        public string Platform { get; set; } = "desktop-windows";
        public string Architecture { get; set; } = "x64";
        public string PackageType { get; set; } = "installer";
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string? Checksum { get; set; }
        public string? Signature { get; set; }
        public string DownloadUrl { get; set; } = string.Empty;
        public string? StorageKey { get; set; }

        public static ArtifactFormModel FromDto(ReleaseArtifactDto dto)
        {
            return new ArtifactFormModel
            {
                ArtifactId = dto.ArtifactId,
                Platform = dto.Platform,
                Architecture = dto.Architecture,
                PackageType = dto.PackageType,
                FileName = dto.FileName,
                FileSize = dto.FileSize,
                Checksum = dto.Checksum,
                Signature = dto.Signature,
                DownloadUrl = dto.DownloadUrl,
                StorageKey = dto.StorageKey
            };
        }

        public SaveReleaseArtifactRequest ToRequest()
        {
            return new SaveReleaseArtifactRequest
            {
                ArtifactId = ArtifactId,
                Platform = Platform.Trim(),
                Architecture = Architecture.Trim(),
                PackageType = PackageType.Trim(),
                FileName = FileName.Trim(),
                FileSize = FileSize,
                Checksum = NormalizeNullable(Checksum),
                Signature = NormalizeNullable(Signature),
                DownloadUrl = DownloadUrl.Trim(),
                StorageKey = NormalizeNullable(StorageKey)
            };
        }
    }

    /// <summary>
    /// 投放规则表单模型。
    /// </summary>
    public sealed class RolloutFormModel
    {
        public Guid? RolloutId { get; set; }
        public string ReleaseIdString { get; set; } = string.Empty;
        public string Platform { get; set; } = "desktop-windows";
        public string Architecture { get; set; } = "x64";
        public string Channel { get; set; } = "stable";
        public string MinimumSupportedVersion { get; set; } = string.Empty;
        public bool ForceUpdate { get; set; }
        public int RolloutPercent { get; set; } = 100;
        public string AudienceRule { get; set; } = "all";
        public string StartsAtText { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm");
        public string? EndsAtText { get; set; }
        public int Priority { get; set; }
        public bool Enabled { get; set; } = true;
        public bool KillSwitchEnabled { get; set; }

        public static RolloutFormModel CreateEmpty() => new();

        public static RolloutFormModel FromDto(RolloutDto dto)
        {
            return new RolloutFormModel
            {
                RolloutId = dto.RolloutId,
                ReleaseIdString = dto.ReleaseId.ToString(),
                Platform = dto.Platform,
                Architecture = dto.Architecture,
                Channel = dto.Channel,
                MinimumSupportedVersion = dto.MinimumSupportedVersion,
                ForceUpdate = dto.ForceUpdate,
                RolloutPercent = dto.RolloutPercent,
                AudienceRule = dto.AudienceRule,
                StartsAtText = dto.StartsAt.ToString("yyyy-MM-ddTHH:mm"),
                EndsAtText = dto.EndsAt?.ToString("yyyy-MM-ddTHH:mm"),
                Priority = dto.Priority,
                Enabled = dto.Enabled,
                KillSwitchEnabled = dto.KillSwitchEnabled
            };
        }

        public SaveRolloutRequest ToRequest()
        {
            if (string.Equals(ReleaseIdString, "__pending_release__", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("请先完成版本保存后再创建投放。");
            }

            if (!Guid.TryParse(ReleaseIdString, out var releaseId))
            {
                throw new InvalidOperationException("请选择目标 Release");
            }

            return new SaveRolloutRequest
            {
                RolloutId = RolloutId,
                ReleaseId = releaseId,
                Platform = Platform.Trim(),
                Architecture = Architecture.Trim(),
                Channel = Channel.Trim(),
                MinimumSupportedVersion = MinimumSupportedVersion.Trim(),
                ForceUpdate = ForceUpdate,
                RolloutPercent = RolloutPercent,
                AudienceRule = AudienceRule.Trim(),
                StartsAt = ParseDateTimeOrDefault(StartsAtText, DateTime.UtcNow),
                EndsAt = ParseNullableDateTime(EndsAtText),
                Priority = Priority,
                Enabled = Enabled,
                KillSwitchEnabled = KillSwitchEnabled
            };
        }
    }

    private static DateTime ParseDateTimeOrDefault(string? value, DateTime fallback)
    {
        return DateTime.TryParse(value, out var parsed)
            ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
            : fallback;
    }

    private static DateTime? ParseNullableDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParse(value, out var parsed)
            ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
            : null;
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
