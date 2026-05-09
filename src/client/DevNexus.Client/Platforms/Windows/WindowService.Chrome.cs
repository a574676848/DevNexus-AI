#if WINDOWS
using System.Diagnostics;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace DevNexus.Client.Services.Platform;

public partial class WindowService
{
    public void HideTitleBar()
    {
        if (_appWindow == null || _nativeWindow == null)
        {
            return;
        }

        _nativeWindow.ExtendsContentIntoTitleBar = true;
        _nativeWindow.SetTitleBar(null);

        if (_appWindow.TitleBar != null)
        {
            _appWindow.TitleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
            _appWindow.TitleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
            _appWindow.TitleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 0, 0, 0);
            _appWindow.TitleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 100, 100, 100);
            _appWindow.TitleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(20, 0, 0, 0);
            _appWindow.TitleBar.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(255, 0, 0, 0);
        }

        _isTitleBarHidden = true;
    }

    public void EnableBorderlessMode()
    {
        if (_appWindow == null)
        {
            return;
        }

        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = true;
            presenter.IsMinimizable = true;
            presenter.IsMaximizable = true;
        }

        SetDragRegion(32);

        _isBorderlessMode = true;
        _isTitleBarHidden = true;
    }

    public void SetDragRegion(int titleBarHeight)
    {
        if (_appWindow == null || _nonClientInputSrc == null)
        {
            return;
        }

        try
        {
            var windowSize = _appWindow.Size;
            const int buttonsWidth = 138;

            var captionRect = new RectInt32(0, 0, windowSize.Width, titleBarHeight);
            _nonClientInputSrc.SetRegionRects(NonClientRegionKind.Caption, new[] { captionRect });

            var buttonsRect = new RectInt32(windowSize.Width - buttonsWidth, 0, buttonsWidth, titleBarHeight);
            _nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, new[] { buttonsRect });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SetDragRegion failed: {ex.Message}");
        }
    }

    public void StartDrag()
    {
    }
}
#endif
