using DevNexus.Shared.DTOs;
using DevNexus.Client.Shared.Components.Chat;
using Microsoft.AspNetCore.Components.WebView.Maui;

namespace DevNexus.Client.Pages;

/// <summary>
/// Artifact 独立预览窗口页面
/// 用于在独立窗口中预览 Artifact 内容（代码、HTML、图表等）
/// </summary>
public partial class ArtifactPreviewPage : ContentPage
{
    private readonly ArtifactDto _artifact;
    private bool _isMaximized;
    private BlazorWebView? _blazorWebView;

    public ArtifactPreviewPage(ArtifactDto artifact)
    {
        InitializeComponent();
        _artifact = artifact;

        // 设置标题（与分屏标题栏保持一致，不添加" - 预览"后缀）
        var title = artifact.Name ?? "未命名资产";
        Title = title;
        TitleLabel.Text = title;

        // 设置图标
        SetTitleIcon(artifact.Type);

        // 设置图标容器背景色（根据类型）
        SetIconBackground(artifact.Type);

        // 设置按钮悬停效果
        SetupButtonHoverEffects();

        // 加载 Artifact 内容
        LoadArtifactContent();
    }

    /// <summary>
    /// 根据 Artifact 类型设置图标
    /// </summary>
    private void SetTitleIcon(string? type)
    {
        TitleIcon.Text = type?.ToLower() switch
        {
            "html" => "🌐",
            "chart" => "📊",
            "mermaid" => "📈",
            "sql" => "🗃️",
            "json" => "📋",
            "markdown" or "md" => "📝",
            _ => "📄"
        };
    }

    /// <summary>
    /// 根据 Artifact 类型设置图标背景色
    /// </summary>
    private void SetIconBackground(string? type)
    {
        IconBorder.BackgroundColor = type?.ToLower() switch
        {
            "html" => Color.FromArgb("#f0f9ff"),      // 蓝色系
            "chart" => Color.FromArgb("#fef3c7"),     // 黄色系
            "mermaid" => Color.FromArgb("#e0f2fe"),   // 天蓝色系
            "sql" => Color.FromArgb("#dbeafe"),       // 深蓝色系
            "json" => Color.FromArgb("#fce7f3"),      // 粉色系
            "markdown" or "md" => Color.FromArgb("#f3e8ff"), // 紫色系
            _ => Color.FromArgb("#f0f9ff")            // 默认蓝色系
        };
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
    /// 加载 Artifact 内容
    /// </summary>
    private void LoadArtifactContent()
    {
        // 创建 BlazorWebView 来承载 ArtifactSplitView 组件
        _blazorWebView = new BlazorWebView
        {
            HostPage = "wwwroot/index.html"
        };

        // 注册根组件，传递 Artifact 数据
        _blazorWebView.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(DevNexus.Client.Shared.Components.Editor.ArtifactPreviewHost),
            Parameters = new Dictionary<string, object?>
            {
                { "Artifact", _artifact }
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
