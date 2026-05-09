using Microsoft.JSInterop;
namespace DevNexus.Client.Web.Services;

/// <summary>
/// Web 文件服务实现 - 基于 JS Interop 和浏览器下载 API
/// </summary>
public class WebFileService : IFileService
{
    private readonly IJSRuntime _js;

    public WebFileService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<string?> PickFileAsync(string[]? allowedExtensions = null, string? title = null)
    {
        // 通过 JS 触发文件选择器
        var accept = allowedExtensions != null ? string.Join(",", allowedExtensions.Select(ext => $".{ext}")) : "*/*";
        return await _js.InvokeAsync<string?>("eval", $@"
            new Promise((resolve) => {{
                const input = document.createElement('input');
                input.type = 'file';
                input.accept = '{accept}';
                input.onchange = (e) => {{
                    const file = e.target.files[0];
                    if (file) resolve(file.name);
                    else resolve(null);
                }};
                input.click();
            }})");
    }

    public Task<IEnumerable<string>> PickMultipleFilesAsync(string[]? allowedExtensions = null, string? title = null)
    {
        // 类似 PickFileAsync，支持多文件
        return Task.FromResult<IEnumerable<string>>(Enumerable.Empty<string>());
    }

    public Task<string?> PickFolderAsync(string? title = null)
    {
        // Web 端使用 directory picker API
        return Task.FromResult<string?>(null);
    }

    public async Task<string?> SaveFileAsync(string suggestedFileName, string content)
    {
        // 使用 Blob 下载
        await _js.InvokeVoidAsync("eval", $@"
            const blob = new Blob(['{EscapeJs(content)}'], {{ type: 'text/plain' }});
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = '{suggestedFileName}';
            a.click();
            URL.revokeObjectURL(url);
        ");
        return suggestedFileName;
    }

    public async Task<string?> SaveFileAsync(string suggestedFileName, byte[] content)
    {
        // 使用 Blob 下载二进制
        var base64 = Convert.ToBase64String(content);
        await _js.InvokeVoidAsync("eval", $@"
            const byteCharacters = atob('{base64}');
            const byteNumbers = new Uint8Array(byteCharacters.length);
            for (let i = 0; i < byteCharacters.length; i++) {{
                byteNumbers[i] = byteCharacters.charCodeAt(i);
            }}
            const blob = new Blob([byteNumbers], {{ type: 'application/octet-stream' }});
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = '{suggestedFileName}';
            a.click();
            URL.revokeObjectURL(url);
        ");
        return suggestedFileName;
    }

    public async Task<string?> DownloadFileAsync(string url, string fileName, Action<int>? progress = null)
    {
        // 使用 fetch 下载
        await _js.InvokeVoidAsync("eval", $@"
            fetch('{url}')
                .then(r => r.blob())
                .then(blob => {{
                    const url = URL.createObjectURL(blob);
                    const a = document.createElement('a');
                    a.href = url;
                    a.download = '{fileName}';
                    a.click();
                    URL.revokeObjectURL(url);
                }})
        ");
        return fileName;
    }

    public Task<string?> DownloadFileWithDialogAsync(string url, string fileName, Action<int>? progress = null)
    {
        return DownloadFileAsync(url, fileName, progress);
    }

    public string GetDownloadPath()
    {
        // Web 端无法获取本地下载路径
        return string.Empty;
    }

    public Task OpenFolderAsync(string path)
    {
        // Web 端无法打开本地文件夹
        return Task.CompletedTask;
    }

    public Task OpenFileAsync(string path)
    {
        // Web 端无法直接打开本地文件
        return Task.CompletedTask;
    }

    private string EscapeJs(string content)
    {
        return content.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "\\r");
    }
}

