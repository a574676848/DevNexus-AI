using DevNexus.Client.Shared.Components.Pages.Analytics;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.DependencyInjection;

namespace DevNexus.Client.Pages;

public partial class AuditDashboardPageHost : ContentPage
{
    private readonly Guid _userId;
    private readonly string _displayName;
    private readonly IWindowService? _windowService;

    public AuditDashboardPageHost(Guid userId, string displayName)
    {
        InitializeComponent();
        
        _userId = userId;
        _displayName = displayName;
        TitleLabel.Text = $"AI 使用与审计 - {displayName}";
        
        _windowService = IPlatformApplication.Current?.Services.GetRequiredService<IWindowService>();

        // 设置按钮悬停效果
        SetupButtonHoverEffects();

        // 初始化 Blazor 内容
        InitializeBlazor();
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

    private void InitializeBlazor()
    {
        var bwv = new BlazorWebView
        {
            HostPage = "wwwroot/index.html"
        };

        bwv.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(DevNexus.Client.Shared.Components.Pages.Analytics.AuditDashboard),
            Parameters = new Dictionary<string, object?>
            {
                { "TargetUserId", _userId }
            }
        });

        ContentContainer.Children.Add(bwv);
    }

    #region 窗口控制事件

    private async void OnMinimizeClicked(object sender, EventArgs e)
    {
        if (_windowService != null)
            await _windowService.MinimizeAsync();
    }

    private async void OnMaximizeClicked(object sender, EventArgs e)
    {
        if (_windowService != null)
        {
            await _windowService.ToggleMaximizeAsync();
            UpdateMaximizeIcon();
        }
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        if (Application.Current != null)
            Application.Current.CloseWindow(this.Window);
    }

    private void UpdateMaximizeIcon()
    {
        if (_windowService != null)
        {
            MaximizeButton.Text = _windowService.IsMaximized ? "❐" : "☐";
        }
    }

    #endregion
}

