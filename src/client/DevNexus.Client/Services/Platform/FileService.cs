using System.Text;
namespace DevNexus.Client.Services.Platform;

/// <summary>
/// MAUI 跨平台文件服务实现
/// </summary>
public class FileService : IFileService
{
    /// <inheritdoc />
    public async Task<string?> PickFileAsync(string[]? allowedExtensions = null, string? title = null)
    {
        try
        {
            var options = new PickOptions
            {
                PickerTitle = title ?? "选择文件"
            };

            if (allowedExtensions != null && allowedExtensions.Length > 0)
            {
                options.FileTypes = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, allowedExtensions },
                        { DevicePlatform.macOS, allowedExtensions },
                        { DevicePlatform.iOS, allowedExtensions },
                        { DevicePlatform.Android, allowedExtensions }
                    });
            }

            var result = await FilePicker.Default.PickAsync(options);
            return result?.FullPath;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> PickMultipleFilesAsync(string[]? allowedExtensions = null, string? title = null)
    {
        try
        {
            var options = new PickOptions
            {
                PickerTitle = title ?? "选择文件"
            };

            if (allowedExtensions != null && allowedExtensions.Length > 0)
            {
                options.FileTypes = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, allowedExtensions },
                        { DevicePlatform.macOS, allowedExtensions },
                        { DevicePlatform.iOS, allowedExtensions },
                        { DevicePlatform.Android, allowedExtensions }
                    });
            }

            var results = await FilePicker.Default.PickMultipleAsync(options);
            return results?.Where(r => r?.FullPath != null).Select(r => r!.FullPath) ?? Enumerable.Empty<string>();
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }

    /// <inheritdoc />
    public Task<string?> PickFolderAsync(string? title = null)
    {
        // MAUI 不直接支持 FolderPicker，需要使用平台特定实现
        // 暂时返回下载目录作为替代
        return Task.FromResult<string?>(GetDownloadPath());
    }

    /// <inheritdoc />
    public async Task<string?> SaveFileAsync(string suggestedFileName, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return await SaveFileAsync(suggestedFileName, bytes);
    }

    /// <inheritdoc />
    public async Task<string?> SaveFileAsync(string suggestedFileName, byte[] content)
    {
        try
        {
            var filePath = Path.Combine(GetDownloadPath(), suggestedFileName);
            await File.WriteAllBytesAsync(filePath, content);
            return filePath;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<string?> DownloadFileAsync(string url, string fileName, Action<int>? progress = null)
    {
        try
        {
            var filePath = Path.Combine(GetDownloadPath(), fileName);
            return await DownloadToPathAsync(url, filePath, progress);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<string?> DownloadFileWithDialogAsync(string url, string fileName, Action<int>? progress = null)
    {
        try
        {
#if WINDOWS
            var filePath = await PickSavePathAsync(fileName);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return null;
            }

            return await DownloadToPathAsync(url, filePath, progress);
#else
            return await DownloadFileAsync(url, fileName, progress);
#endif
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public string GetDownloadPath()
    {
#if WINDOWS
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
#else
        return FileSystem.CacheDirectory ?? FileSystem.AppDataDirectory;
#endif
    }

#if WINDOWS
    private static async Task<string?> PickSavePathAsync(string suggestedFileName)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker
            {
                SuggestedFileName = suggestedFileName
            };

            picker.FileTypeChoices.Add("所有文件", new List<string> { ".*" });

            if (Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView is Microsoft.UI.Xaml.Window window)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            }

            var file = await picker.PickSaveFileAsync();
            return file?.Path;
        }
        catch
        {
            return null;
        }
    }
#endif

    private static async Task<string?> DownloadToPathAsync(string url, string filePath, Action<int>? progress)
    {
        using var client = new HttpClient();
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        var downloadedBytes = 0L;

        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
        await using var contentStream = await response.Content.ReadAsStreamAsync();

        var buffer = new byte[8192];
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
            downloadedBytes += bytesRead;

            if (totalBytes > 0)
            {
                var percentage = (int)(downloadedBytes * 100 / totalBytes);
                progress?.Invoke(percentage);
            }
        }

        return filePath;
    }

    /// <inheritdoc />
    public async Task OpenFolderAsync(string path)
    {
        try
        {
            // 使用 MAUI Launcher 打开文件夹
            await Launcher.Default.OpenAsync(path);
        }
        catch
        {
            // 如果 Launcher 失败，此处暂无其他跨平台替代方案
        }
    }

    /// <inheritdoc />
    public async Task OpenFileAsync(string path)
    {
        try
        {
            // 使用 MAUI Launcher 打开文件
            await Launcher.Default.OpenAsync(path);
        }
        catch (Exception)
        {
            // 打开文件失败时静默处理
        }
    }
}

