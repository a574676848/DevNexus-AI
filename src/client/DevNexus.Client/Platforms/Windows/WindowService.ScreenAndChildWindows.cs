#if WINDOWS
using System.Diagnostics;
using System.Runtime.InteropServices;
using DevNexus.Client.Pages;
using DevNexus.Shared.DTOs;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;

namespace DevNexus.Client.Services.Platform;

public partial class WindowService
{
    public ScreenInfo GetScreenInfo()
    {
        if (_appWindow == null)
        {
            return CreateFallbackScreenInfo();
        }

        var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
        if (displayArea == null)
        {
            return CreateFallbackScreenInfo();
        }

        var dpi = GetDpiForWindow(_windowHandle);
        var scaleFactor = dpi / 96.0;
        var physicalWidth = (int)(displayArea.OuterBounds.Width * scaleFactor);
        var physicalHeight = (int)(displayArea.OuterBounds.Height * scaleFactor);
        var screenType = ScreenInfo.DetermineScreenType(physicalWidth, physicalHeight);

        return new ScreenInfo
        {
            Width = displayArea.OuterBounds.Width,
            Height = displayArea.OuterBounds.Height,
            WorkAreaWidth = displayArea.WorkArea.Width,
            WorkAreaHeight = displayArea.WorkArea.Height,
            ScaleFactor = scaleFactor,
            ScreenType = screenType
        };
    }

    public void SetAdaptiveSize(string windowType)
    {
        var screenInfo = GetScreenInfo();
        int width;
        int height;

        if (windowType.Equals("login", StringComparison.OrdinalIgnoreCase))
        {
            (width, height) = screenInfo.ScreenType switch
            {
                "4K" => (480, 720),
                "2K" => (400, 600),
                _ => (320, 480)
            };
        }
        else
        {
            (width, height) = screenInfo.ScreenType switch
            {
                "4K" => (
                    Math.Min(1650, (int)(screenInfo.WorkAreaWidth * 0.7)),
                    Math.Min(1100, (int)(screenInfo.WorkAreaHeight * 0.8))
                ),
                "2K" => (
                    Math.Min(1400, (int)(screenInfo.WorkAreaWidth * 0.75)),
                    Math.Min(900, (int)(screenInfo.WorkAreaHeight * 0.85))
                ),
                _ => (
                    Math.Min(1100, (int)(screenInfo.WorkAreaWidth * 0.8)),
                    Math.Min(750, (int)(screenInfo.WorkAreaHeight * 0.85))
                )
            };
        }

        SetSize(width, height);
    }

    public void OpenWebWindow(string url, string title)
    {
        OpenChildWindow(
            new WebViewPage(url, title),
            title,
            width: 1000,
            height: 700,
            minimumWidth: 600,
            minimumHeight: 400);
    }

    public void OpenArtifactWindow(ArtifactDto artifact)
    {
        var title = $"{artifact.Name ?? "Artifact"} - 预览";
        OpenChildWindow(
            new ArtifactPreviewPage(artifact),
            title,
            width: 1200,
            height: 800,
            minimumWidth: 800,
            minimumHeight: 600);
    }

    public void OpenSwarmWindow(Guid sessionId)
    {
        OpenChildWindow(
            new SwarmPreviewPage(sessionId),
            $"Swarm Cluster - {sessionId}",
            width: 1200,
            height: 800,
            minimumWidth: 800,
            minimumHeight: 600);
    }

    public void OpenAuditWindow(Guid userId, string displayName)
    {
        OpenChildWindow(
            new AuditDashboardPageHost(userId, displayName),
            $"AI 使用与审计 - {displayName}",
            width: 1100,
            height: 850,
            minimumWidth: 900,
            minimumHeight: 600);
    }

    private void OpenChildWindow(
        Page page,
        string title,
        double width,
        double height,
        double minimumWidth,
        double minimumHeight)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var newWindow = new Microsoft.Maui.Controls.Window(page)
                {
                    Title = title,
                    Width = width,
                    Height = height,
                    MinimumWidth = minimumWidth,
                    MinimumHeight = minimumHeight
                };

                newWindow.Created += (_, _) =>
                {
                    newWindow.Dispatcher.Dispatch(() => ConfigureChildWindow(newWindow));
                };

                Application.Current?.OpenWindow(newWindow);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OpenChildWindow failed: {ex.Message}");
            }
        });
    }

    private void ConfigureChildWindow(Microsoft.Maui.Controls.Window window)
    {
        try
        {
            var nativeWindow = window.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
            if (nativeWindow == null)
            {
                return;
            }

            var hwnd = WindowNative.GetWindowHandle(nativeWindow);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow?.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(false, false);
                presenter.IsResizable = true;
                presenter.IsMinimizable = true;
                presenter.IsMaximizable = true;
            }

            var displayArea = DisplayArea.GetFromWindowId(appWindow!.Id, DisplayAreaFallback.Primary);
            if (displayArea != null)
            {
                var centerX = (displayArea.WorkArea.Width - appWindow.Size.Width) / 2;
                var centerY = (displayArea.WorkArea.Height - appWindow.Size.Height) / 2;
                appWindow.Move(new PointInt32(centerX, centerY));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ConfigureChildWindow failed: {ex.Message}");
        }
    }

    private static ScreenInfo CreateFallbackScreenInfo()
    {
        return new ScreenInfo
        {
            Width = 1920,
            Height = 1080,
            WorkAreaWidth = 1920,
            WorkAreaHeight = 1040,
            ScaleFactor = 1.0,
            ScreenType = "HD"
        };
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
#endif
