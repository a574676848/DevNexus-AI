using DevNexus.Client.Shared.Abstractions;

namespace DevNexus.Client.Services.System;

/// <summary>
/// 基于安全存储的更新版本偏好存储实现。
/// </summary>
public sealed class UpdatePreferenceStore : IUpdatePreferenceStore
{
    private const string IgnoredVersionPrefix = "DevNexus.Update.Ignored.";
    private const string SnoozedVersionPrefix = "DevNexus.Update.SnoozedUntil.";

    private readonly ISecureStorageService _secureStorageService;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public UpdatePreferenceStore(ISecureStorageService secureStorageService)
    {
        _secureStorageService = secureStorageService;
    }

    /// <inheritdoc />
    public Task IgnoreVersionAsync(string version)
    {
        _ = _secureStorageService.RemoveAsync(GetSnoozedVersionKey(version));
        return _secureStorageService.SetAsync(GetIgnoredVersionKey(version), "true");
    }

    /// <inheritdoc />
    public Task SnoozeVersionAsync(string version, TimeSpan duration)
    {
        var until = DateTimeOffset.UtcNow.Add(duration).ToUnixTimeMilliseconds();
        return _secureStorageService.SetAsync(GetSnoozedVersionKey(version), until.ToString());
    }

    /// <inheritdoc />
    public async Task<bool> ShouldSkipVersionAsync(string version)
    {
        if (await IsIgnoredVersionAsync(version))
        {
            return true;
        }

        var snoozedUntil = await GetSnoozedUntilAsync(version);
        if (snoozedUntil.HasValue && snoozedUntil.Value > DateTimeOffset.UtcNow)
        {
            return true;
        }

        if (snoozedUntil.HasValue && snoozedUntil.Value <= DateTimeOffset.UtcNow)
        {
            _ = _secureStorageService.RemoveAsync(GetSnoozedVersionKey(version));
        }

        return false;
    }

    private async Task<bool> IsIgnoredVersionAsync(string version)
    {
        var value = await _secureStorageService.GetAsync(GetIgnoredVersionKey(version));
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<DateTimeOffset?> GetSnoozedUntilAsync(string version)
    {
        var value = await _secureStorageService.GetAsync(GetSnoozedVersionKey(version));
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return long.TryParse(value, out var ticks)
            ? DateTimeOffset.FromUnixTimeMilliseconds(ticks)
            : null;
    }

    private static string GetIgnoredVersionKey(string version)
    {
        return $"{IgnoredVersionPrefix}{version}";
    }

    private static string GetSnoozedVersionKey(string version)
    {
        return $"{SnoozedVersionPrefix}{version}";
    }
}
