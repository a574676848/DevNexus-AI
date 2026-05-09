using Microsoft.JSInterop;
namespace DevNexus.Client.Web.Services;

/// <summary>
/// Web 安全存储实现 - 基于 localStorage
/// 支持 JSInterop 和备选方案
/// </summary>
public class WebSecureStorageService : ISecureStorageService
{
    private readonly IJSRuntime _js;

    public WebSecureStorageService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<string?> GetAsync(string key)
    {
        try
        {
            return await _js.InvokeAsync<string?>("window.storage.get", key);
        }
        catch (JSException jsEx) when (jsEx.Message.Contains("window.storage"))
        {
            // 备选方案：如果 window.storage 不可用，尝试使用 localStorage 直接访问
            try
            {
                return await _js.InvokeAsync<string?>("eval", $"localStorage.getItem('{EscapeString(key)}')", null);
            }
            catch
            {
                return null;
            }
        }
    }

    public async Task SetAsync(string key, string value)
    {
        try
        {
            await _js.InvokeVoidAsync("window.storage.set", key, value);
        }
        catch (JSException jsEx) when (jsEx.Message.Contains("window.storage"))
        {
            // 备选方案
            try
            {
                await _js.InvokeVoidAsync("eval", $"localStorage.setItem('{EscapeString(key)}', '{EscapeString(value)}')", null);
            }
            catch
            {
                // 忽略备选方案的错误
            }
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _js.InvokeVoidAsync("window.storage.remove", key);
        }
        catch (JSException jsEx) when (jsEx.Message.Contains("window.storage"))
        {
            try
            {
                await _js.InvokeVoidAsync("eval", $"localStorage.removeItem('{EscapeString(key)}')", null);
            }
            catch
            {
                // 忽略备选方案的错误
            }
        }
    }

    private static string EscapeString(string input)
    {
        return input.Replace("\\", "\\\\").Replace("'", "\\'");
    }
}
