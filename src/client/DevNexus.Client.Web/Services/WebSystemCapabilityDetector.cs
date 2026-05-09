using Microsoft.JSInterop;
namespace DevNexus.Client.Web.Services;

/// <summary>
/// Web 系统能力检测器实现
/// </summary>
public class WebSystemCapabilityDetector : ISystemCapabilityDetector
{
    private readonly IJSRuntime _js;

    public WebSystemCapabilityDetector(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<bool> IsClipboardSupportedAsync()
    {
        return await _js.InvokeAsync<bool>("eval", "'clipboard' in navigator");
    }

    public async Task<bool> IsClipboardReadSupportedAsync()
    {
        return await _js.InvokeAsync<bool>("eval", "'clipboard' in navigator && 'readText' in navigator.clipboard");
    }
}

