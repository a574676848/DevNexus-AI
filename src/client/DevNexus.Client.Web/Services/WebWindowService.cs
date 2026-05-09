using Microsoft.JSInterop;
using System.Runtime.InteropServices;
using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Web.Services;

/// <summary>
/// Web 窗口服务实现 - 基于 JS Interop 的 Web 窗口控制
/// </summary>
public class WebWindowService : IWindowService
{
    private readonly IJSRuntime _js;
    private bool _isMaximized;

    public bool IsMaximized => _isMaximized;
    public bool IsTitleBarHidden => false;
    public bool IsBorderlessMode => false;
    public event Action<bool>? OnMaximizedChanged;

    public WebWindowService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task MinimizeAsync()
    {
        // Web 端无法最小化浏览器窗口，使用页面滚动到顶部替代
        await _js.InvokeVoidAsync("eval", "window.scrollTo(0, 0)");
    }

    public async Task MaximizeAsync()
    {
        _isMaximized = true;
        // Web 端不需要全屏，仅更新状态
        // 移除 requestFullscreen() 调用以避免进入浏览器全屏模式
        await Task.Delay(0); // 占位符，保持异步方法签名
        OnMaximizedChanged?.Invoke(true);
    }

    public async Task ToggleMaximizeAsync()
    {
        if (_isMaximized)
            await RestoreAsync();
        else
            await MaximizeAsync();
    }

    public async Task RestoreAsync()
    {
        _isMaximized = false;
        // Web 端不需要退出全屏，仅更新状态
        await Task.Delay(0); // 占位符，保持异步方法签名
        OnMaximizedChanged?.Invoke(false);
    }

    public async Task CloseAsync()
    {
        // Web 端无法关闭浏览器窗口，提示用户
        await _js.InvokeVoidAsync("eval", "if(confirm('确定要离开吗？')) window.close()");
    }

    public void HideTitleBar()
    {
        // Web 端不支持隐藏标题栏
    }

    public void EnableBorderlessMode()
    {
        // Web 端不支持无边框模式
    }

    public void StartDrag()
    {
        // Web 端不支持窗口拖拽（由浏览器控制）
    }

    public void SetDragRegion(int titleBarHeight)
    {
        // Web 端不支持设置拖拽区域
    }

    public void SetSize(int width, int height)
    {
        // Web 端通过 CSS 控制窗口大小
        _js.InvokeVoidAsync("eval", $"document.body.style.width = '{width}px'; document.body.style.height = '{height}px'");
    }

    public void CenterOnScreen()
    {
        // Web 端无法控制窗口位置
    }

    public void SetAlwaysOnTop(bool alwaysOnTop)
    {
        // Web 端不支持置顶
    }

    public ScreenInfo GetScreenInfo()
    {
        // 使用浏览器 Screen API
        return new ScreenInfo
        {
            Width = 1920, // 从 JS 获取
            Height = 1080,
            WorkAreaWidth = 1920,
            WorkAreaHeight = 1040,
            ScaleFactor = 1.0,
            ScreenType = "HD"
        };
    }

    public void SetAdaptiveSize(string windowType)
    {
        // Web 端通过 CSS 响应式布局实现
    }

    public void OpenWebWindow(string url, string title)
    {
        _js.InvokeVoidAsync("eval", $"window.open('{url}', '_blank')");
    }

    public void OpenArtifactWindow(ArtifactDto artifact)
    {
        // Web 端使用新标签页打开 Artifact
        // 为保证实时性（处理未入库资产），先存入 LocalStorage
        var json = System.Text.Json.JsonSerializer.Serialize(artifact);
        _js.InvokeVoidAsync("localStorage.setItem", $"artifact_temp_{artifact.ArtifactId}", json);
        
        _js.InvokeVoidAsync("eval", $"window.open('/artifacts/{artifact.ArtifactId}', '_blank')");
    }

    public void OpenSwarmWindow(Guid sessionId)
    {
        _js.InvokeVoidAsync("eval", $"window.open('/swarm/{sessionId}', '_blank')");
    }

    public void OpenAuditWindow(Guid userId, string displayName)
    {
        _js.InvokeVoidAsync("eval", $"window.open('/analytics/audit?user={userId}', '_blank')");
    }

    public Task SetApplicationRestartState(bool restart)
    {
        return Task.CompletedTask;
    }

    public void CloseApplication()
    {
        _js.InvokeVoidAsync("eval", "window.close()");
    }
}

