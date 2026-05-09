using System.Security;
using System.Security.Cryptography;
using DevNexus.Client.Shared.Abstractions;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;

namespace DevNexus.Client.Services.System;

/// <summary>
/// 更新包处理器实现。
/// 将下载与校验细节从协调器中拆分出来。
/// </summary>
public sealed class UpdatePackageExecutor : IUpdatePackageExecutor
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UpdatePackageExecutor> _logger;
    private readonly string _updateCachePath;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public UpdatePackageExecutor(
        IHttpClientFactory httpClientFactory,
        ILogger<UpdatePackageExecutor> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;

        _updateCachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DevNexus",
            "Updates");

        Directory.CreateDirectory(_updateCachePath);
    }

    /// <inheritdoc />
    public Task<string> DownloadPackageAsync(
        UpdateInfo update,
        Action<int>? progress,
        CancellationToken cancellationToken = default)
    {
        return DownloadUpdateAsync(update, progress, cancellationToken);
    }

    /// <inheritdoc />
    public async Task VerifyPackageAsync(string packagePath, UpdateInfo update, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(update.Checksum))
        {
            return;
        }

        var valid = await VerifyChecksumAsync(packagePath, update.Checksum, cancellationToken);
        if (!valid)
        {
            throw new SecurityException("更新包校验失败，请重新下载");
        }
    }

    private async Task<string> DownloadUpdateAsync(UpdateInfo update, Action<int>? progress, CancellationToken cancellationToken)
    {
        var downloadDir = Path.Combine(_updateCachePath, update.Version);
        Directory.CreateDirectory(downloadDir);

        var fileName = ResolvePackageFileName(update);

        var downloadPath = Path.Combine(downloadDir, fileName);
        var resolvedDownloadUrl = ResolveDownloadUrl(update.DownloadUrl, update.Version);

        if (File.Exists(downloadPath))
        {
            var fileInfo = new FileInfo(downloadPath);
            if (update.FileSize > 0 && fileInfo.Length == update.FileSize)
            {
                _logger.LogInformation("[UpdatePackageExecutor] 更新包已存在，跳过下载");
                progress?.Invoke(100);
                return downloadPath;
            }
        }

        using var response = await _httpClient.GetAsync(
            resolvedDownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        var downloadedBytes = 0L;

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var fileStream = File.Create(downloadPath);

        var buffer = new byte[81920];
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            downloadedBytes += bytesRead;

            if (totalBytes > 0)
            {
                var percent = (int)(downloadedBytes * 100 / totalBytes);
                progress?.Invoke(percent);
            }
        }

        return downloadPath;
    }

    private async Task<bool> VerifyChecksumAsync(string filePath, string expectedChecksum, CancellationToken cancellationToken)
    {
        try
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
            var actualChecksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            var normalizedExpectedChecksum = expectedChecksum.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
                ? expectedChecksum["sha256:".Length..]
                : expectedChecksum;

            var isValid = actualChecksum.Equals(normalizedExpectedChecksum, StringComparison.OrdinalIgnoreCase);

            if (!isValid)
            {
                _logger.LogWarning(
                    "[UpdatePackageExecutor] 校验和不匹配 | Expected={Expected} Actual={Actual}",
                    normalizedExpectedChecksum,
                    actualChecksum);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UpdatePackageExecutor] 校验和验证失败");
            return false;
        }
    }

    private static string ResolveDownloadUrl(string downloadUrl, string version)
    {
        return downloadUrl.Replace("{version}", version, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolvePackageFileName(UpdateInfo update)
    {
        if (!string.IsNullOrWhiteSpace(update.FileName))
        {
            return update.FileName.Trim();
        }

        if (Uri.TryCreate(update.DownloadUrl, UriKind.Absolute, out var downloadUri))
        {
            var candidate = Path.GetFileName(downloadUri.LocalPath);
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return update.PackageType switch
        {
            "portable" => "DevNexus.AI.Portable.zip",
            "diff" => "DevNexus.AI.Diff.pkg",
            _ => "DevNexus.AI.Setup.exe"
        };
    }
}
