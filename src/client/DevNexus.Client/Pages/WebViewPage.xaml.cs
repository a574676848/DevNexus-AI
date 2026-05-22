namespace DevNexus.Client.Pages;

/// <summary>
/// 独立 WebView 窗口页面
/// 用于显示 Hangfire、Seq 等 Web 内容
/// </summary>
public partial class WebViewPage : ContentPage
{
    private readonly string _url;
#if WINDOWS
    private bool _isMaximized;
#endif

    public WebViewPage(string url, string title)
    {
        InitializeComponent();
        _url = url;
        Title = title;
        TitleLabel.Text = title;

        // 设置图标
        SetTitleIcon(title);

        // 加载 URL
        WebContent.Source = new UrlWebViewSource { Url = url };

        // 监听导航事件
        WebContent.Navigating += OnWebViewNavigating;
        WebContent.Navigated += OnWebViewNavigated;
    }

    /// <summary>
    /// 根据标题设置图标
    /// </summary>
    private void SetTitleIcon(string title)
    {
        // 使用 Font Awesome 的 Unicode 字符或默认图标
        // 由于 MAUI 不支持 FA 字体图标直接在 Image 中使用，这里留空
        // 可以后续添加 SVG 图标资源
    }

    /// <summary>
    /// WebView 开始导航
    /// </summary>
    private void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
    }

    /// <summary>
    /// WebView 导航完成
    /// </summary>
    private void OnWebViewNavigated(object? sender, WebNavigatedEventArgs e)
    {
        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
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
