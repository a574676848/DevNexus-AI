namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// 文件服务接口 - 提供跨平台文件操作能力
/// </summary>
public interface IFileService
{
    Task<string?> PickFileAsync(string[]? allowedExtensions = null, string? title = null);
    Task<IEnumerable<string>> PickMultipleFilesAsync(string[]? allowedExtensions = null, string? title = null);
    Task<string?> PickFolderAsync(string? title = null);
    Task<string?> SaveFileAsync(string suggestedFileName, string content);
    Task<string?> SaveFileAsync(string suggestedFileName, byte[] content);
    Task<string?> DownloadFileAsync(string url, string fileName, Action<int>? progress = null);
    Task<string?> DownloadFileWithDialogAsync(string url, string fileName, Action<int>? progress = null);
    string GetDownloadPath();
    Task OpenFolderAsync(string path);
    Task OpenFileAsync(string path);
}

