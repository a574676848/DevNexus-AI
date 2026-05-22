#if MACCATALYST
using DevNexus.Client.Pages;

namespace DevNexus.Client.Services.Platform;

/// <summary>
/// Mac Catalyst 平台窗口服务实现。
/// 使用 MAUI 跨平台窗口 API，无法等价映射的 Windows 专属行为会安全降级。
/// </summary>
public partial class WindowService : IWindowService, IWindowLifecycleHandler
{
    private Microsoft.Maui.Controls.Window? _window;
    private bool _isMaximized;
    private bool _isTitleBarHidden;
    private bool _isBorderlessMode;

    private const double DefaultWidth = 1280;
    private const double DefaultHeight = 860;

    public bool IsMaximized => _isMaximized;

    public bool IsTitleBarHidden => _isTitleBarHidden;

    public bool IsBorderlessMode => _isBorderlessMode;

    public event Action<bool>? OnMaximizedChanged;

    public void Initialize(Microsoft.Maui.Controls.Window window)
    {
        _window = window;
    }

    public Task MinimizeAsync()
    {
        // Mac Catalyst 当前不通过 MAUI 暴露窗口最小化 API，降级为无操作。
        return Task.CompletedTask;
    }

    public Task MaximizeAsync()
    {
        var window = GetWindow();
        if (window == null)
        {
            return Task.CompletedTask;
        }

        var screen = GetScreenInfo();
        window.Width = Math.Max(window.MinimumWidth, screen.WorkAreaWidth);
        window.Height = Math.Max(window.MinimumHeight, screen.WorkAreaHeight);
        SetMaximized(true);
        return Task.CompletedTask;
    }

    public Task ToggleMaximizeAsync()
    {
        return _isMaximized ? RestoreAsync() : MaximizeAsync();
    }

    public Task RestoreAsync()
    {
        var window = GetWindow();
        if (window == null)
        {
            return Task.CompletedTask;
        }

        window.Width = Math.Max(window.MinimumWidth, DefaultWidth);
        window.Height = Math.Max(window.MinimumHeight, DefaultHeight);
        SetMaximized(false);
        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        var window = GetWindow();
        if (window != null)
        {
            Application.Current?.CloseWindow(window);
        }

        return Task.CompletedTask;
    }

    public void HideTitleBar()
    {
        _isTitleBarHidden = true;
    }

    public void EnableBorderlessMode()
    {
        _isBorderlessMode = true;
    }

    public void StartDrag()
    {
    }

    public void SetDragRegion(int titleBarHeight)
    {
    }

    public void SetSize(int width, int height)
    {
        var window = GetWindow();
        if (window == null)
        {
            return;
        }

        window.Width = Math.Max(window.MinimumWidth, width);
        window.Height = Math.Max(window.MinimumHeight, height);
    }

    public void CenterOnScreen()
    {
        // MAUI 当前未对 Mac Catalyst 暴露标准化居中 API。
    }

    public void SetAlwaysOnTop(bool alwaysOnTop)
    {
    }

    public ScreenInfo GetScreenInfo()
    {
        var display = DeviceDisplay.Current.MainDisplayInfo;
        var scale = display.Density <= 0 ? 1 : display.Density;
        var width = (int)Math.Round(display.Width / scale);
        var height = (int)Math.Round(display.Height / scale);

        return new ScreenInfo
        {
            Width = width,
            Height = height,
            WorkAreaWidth = width,
            WorkAreaHeight = height,
            ScaleFactor = scale,
            ScreenType = ScreenInfo.DetermineScreenType(width, height)
        };
    }

    public void SetAdaptiveSize(string windowType)
    {
        var screen = GetScreenInfo();
        var (width, height) = windowType.ToLowerInvariant() switch
        {
            "login" => (420, 620),
            "audit" => (1100, 850),
            "artifact" or "swarm" => (1200, 820),
            "web" => (1100, 760),
            _ => (Math.Min(screen.WorkAreaWidth - 120, 1380), Math.Min(screen.WorkAreaHeight - 120, 920))
        };

        SetSize(width, height);
    }

    public void OpenWebWindow(string url, string title)
    {
        OpenWindow(() => new WebViewPage(url, title), title, 1000, 700, 600, 400);
    }

    public void OpenArtifactWindow(DevNexus.Shared.DTOs.ArtifactDto artifact)
    {
        var title = $"{artifact.Name ?? "Artifact"} - 预览";
        OpenWindow(() => new ArtifactPreviewPage(artifact), title, 1200, 800, 800, 600);
    }

    public void OpenSwarmWindow(Guid sessionId)
    {
        var title = $"Swarm Cluster - {sessionId}";
        OpenWindow(() => new SwarmPreviewPage(sessionId), title, 1200, 800, 800, 600);
    }

    public void OpenAuditWindow(Guid userId, string displayName)
    {
        var title = $"AI 使用与审计 - {displayName}";
        OpenWindow(() => new AuditDashboardPageHost(userId, displayName), title, 1100, 850, 900, 600);
    }

    public Task SetApplicationRestartState(bool restart)
    {
        return Task.CompletedTask;
    }

    public void CloseApplication()
    {
        if (Application.Current == null)
        {
            return;
        }

        var windows = Application.Current.Windows.ToList();
        foreach (var window in windows)
        {
            Application.Current.CloseWindow(window);
        }
    }

    private Microsoft.Maui.Controls.Window? GetWindow()
    {
        return _window ?? Application.Current?.Windows.FirstOrDefault();
    }

    private void SetMaximized(bool value)
    {
        if (_isMaximized == value)
        {
            return;
        }

        _isMaximized = value;
        OnMaximizedChanged?.Invoke(value);
    }

    private static void OpenWindow(
        Func<Page> pageFactory,
        string title,
        double width,
        double height,
        double minWidth,
        double minHeight)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var newWindow = new Microsoft.Maui.Controls.Window(pageFactory())
                {
                    Title = title,
                    Width = width,
                    Height = height,
                    MinimumWidth = minWidth,
                    MinimumHeight = minHeight
                };

                Application.Current?.OpenWindow(newWindow);
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Debug.WriteLine($"[MacWindowService] OpenWindow failed: {ex.Message}");
            }
        });
    }
}
#endif
