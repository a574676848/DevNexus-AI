using DevNexus.Client.Shared.Components.Swarm;
using Microsoft.AspNetCore.Components.WebView.Maui;

namespace DevNexus.Client.Pages;

/// <summary>
/// Swarm 独立预览窗口页面
/// 用于在独立窗口中全屏查看 Swarm 集群执行状态
/// </summary>
public partial class SwarmPreviewPage : ContentPage
{
    private readonly Guid _sessionId;
#if WINDOWS
    private bool _isMaximized;
#endif
    private BlazorWebView? _blazorWebView;

    public SwarmPreviewPage(Guid sessionId)
    {
        InitializeComponent();
        _sessionId = sessionId;

        // 设置标题
        var title = $"Swarm Cluster - {sessionId}";
        Title = title;
        TitleLabel.Text = "Swarm Cluster";

        // 设置图标背景色
        IconBorder.BackgroundColor = Color.FromArgb("#f0fdf4"); // 浅绿色背景匹配主体色

        // 设置按钮悬停效果
        SetupButtonHoverEffects();

        // 加载 Swarm 内容
        LoadSwarmContent();
    }

    /// <summary>
    /// 设置按钮悬停效果
    /// </summary>
    private void SetupButtonHoverEffects()
    {
        // 获取所有按钮 Border
        var buttons = new[]
        {
            (MinimizeBorder, false),
            (MaximizeBorder, false),
            (CloseBorder, true) // 关闭按钮特殊处理
        };

        foreach (var (border, isClose) in buttons)
        {
            if (border == null) continue;

            // 添加指针进入事件
            var pointerGesture = new PointerGestureRecognizer();
            pointerGesture.PointerEntered += (s, e) => OnButtonPointerEntered(border, isClose);
            pointerGesture.PointerExited += (s, e) => OnButtonPointerExited(border);
            border.GestureRecognizers.Add(pointerGesture);
        }
    }

    /// <summary>
    /// 按钮指针进入事件
    /// </summary>
    private async void OnButtonPointerEntered(Border border, bool isClose)
    {
        // 设置悬停背景色
        border.BackgroundColor = isClose
            ? Color.FromArgb("#fee2e2")  // 关闭按钮红色
            : Color.FromArgb("#e8e8e8"); // 其他按钮灰色

        // 设置文字颜色
        if (border.Content is Label label)
        {
            label.TextColor = isClose
                ? Color.FromArgb("#fa5151")
                : Color.FromArgb("#1a1a1a");
        }

        // 上浮动画
        await border.TranslateToAsync(0, -1, 150, Easing.CubicOut);
    }

    /// <summary>
    /// 按钮指针退出事件
    /// </summary>
    private async void OnButtonPointerExited(Border border)
    {
        // 恢复默认背景色
        border.BackgroundColor = Color.FromArgb("#f5f5f5");

        // 恢复文字颜色
        if (border.Content is Label label)
        {
            label.TextColor = Color.FromArgb("#666666");
        }

        // 恢复位置
        await border.TranslateToAsync(0, 0, 150, Easing.CubicOut);
    }

    /// <summary>
    /// 加载 Swarm 内容
    /// </summary>
    private void LoadSwarmContent()
    {
        // 创建 BlazorWebView 来承载 SwarmMonitorHost 组件
        _blazorWebView = new BlazorWebView
        {
            HostPage = "wwwroot/index.html"
        };

        // 注册根组件，传递 SessionId
        _blazorWebView.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(DevNexus.Client.Shared.Components.Swarm.SwarmMonitorHost),
            Parameters = new Dictionary<string, object?>
            {
                { "SessionId", _sessionId }
            }
        });

        ContentContainer.Children.Add(_blazorWebView);
    }

    /// <summary>
    /// 页面卸载时清理资源
    /// </summary>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // 清理 BlazorWebView 以防止渲染器错误
        if (_blazorWebView != null)
        {
            try
            {
                ContentContainer.Children.Remove(_blazorWebView);
                _blazorWebView = null;
            }
            catch
            {
                // 忽略清理错误
            }
        }
    }

    /// <summary>
    /// 最小化窗口
    /// </summary>
    private void OnMinimizeClicked(object? sender, EventArgs e)
    {
#if WINDOWS
        var window = this.Window;
        if (window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            if (appWindow?.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                presenter.Minimize();
            }
        }
#endif
    }

    /// <summary>
    /// 最大化/还原窗口
    /// </summary>
    private void OnMaximizeClicked(object? sender, EventArgs e)
    {
#if WINDOWS
        var window = this.Window;
        if (window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            if (appWindow?.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                if (_isMaximized)
                {
                    presenter.Restore();
                    MaximizeButton.Text = "☐";
                }
                else
                {
                    presenter.Maximize();
                    MaximizeButton.Text = "❐";
                }
                _isMaximized = !_isMaximized;
            }
        }
#endif
    }

    /// <summary>
    /// 关闭窗口
    /// </summary>
    private void OnCloseClicked(object? sender, EventArgs e)
    {
        var window = this.Window;
        if (window != null)
        {
            Application.Current?.CloseWindow(window);
        }
    }
}
