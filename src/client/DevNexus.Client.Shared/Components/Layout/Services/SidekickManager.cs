using Microsoft.JSInterop;

namespace DevNexus.Client.Shared.Components.Layout.Services;

/// <summary>
/// 分屏（Sidekick）管理服务 - 负责分屏的相关逻辑
/// </summary>
public class SidekickManager
{
    private readonly IChatState _chatState;
    private readonly IJSRuntime _jsRuntime;

    public event Action? OnStateChanged;

    public SidekickManager(IChatState chatState, IJSRuntime jsRuntime)
    {
        _chatState = chatState;
        _jsRuntime = jsRuntime;
    }

    public bool IsSidekickVisible => _chatState.IsSidekickVisible;

    /// <summary>
    /// 同步分屏状态到 JavaScript
    /// 用于代码块自动折叠/展开功能
    /// </summary>
    public async Task SyncStateToJS()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("eval",
                $"window.devnexus = window.devnexus || {{}}; " +
                $"if (window.devnexus.syncSidekickState) {{ " +
                $"window.devnexus.syncSidekickState({(_chatState.IsSidekickVisible ? "true" : "false")}); " +
                $"}} else {{ " +
                $"window.devnexus.isSidekickVisible = {(_chatState.IsSidekickVisible ? "true" : "false")}; " +
                $"}}");
        }
        catch
        {
            // 忽略 JS 调用失败
            System.Diagnostics.Debug.WriteLine("[SidekickManager] JS 调用失败");
        }
    }

    /// <summary>
    /// 关闭分屏
    /// </summary>
    public void Close()
    {
        _chatState.ToggleSidekick(false);
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// 在访问非聊天页面时自动关闭分屏
    /// </summary>
    public bool TryCloseIfNotOnChatPage(string currentPath)
    {
        var isChatPage = currentPath == "/" || currentPath.StartsWith("/chat");

        if (!isChatPage && _chatState.IsSidekickVisible)
        {
            _chatState.ToggleSidekick(false);
            return true; // 表示关闭了分屏
        }

        return false;
    }
}
