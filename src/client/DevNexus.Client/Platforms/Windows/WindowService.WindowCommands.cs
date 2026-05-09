#if WINDOWS
using System.Diagnostics;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace DevNexus.Client.Services.Platform;

public partial class WindowService
{
    public Task MinimizeAsync()
    {
        if (_appWindow?.Presenter is OverlappedPresenter presenter)
        {
            presenter.Minimize();
        }

        return Task.CompletedTask;
    }

    public Task MaximizeAsync()
    {
        if (_appWindow == null)
        {
            return Task.CompletedTask;
        }

        if (_isBorderlessMode)
        {
            ManualMaximize();
        }
        else if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }

        return Task.CompletedTask;
    }

    public Task RestoreAsync()
    {
        if (_appWindow == null)
        {
            return Task.CompletedTask;
        }

        if (_isBorderlessMode && _isManualMaximized)
        {
            ManualRestore();
        }
        else if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Restore();
        }

        return Task.CompletedTask;
    }

    public Task ToggleMaximizeAsync()
    {
        if (_appWindow == null)
        {
            return Task.CompletedTask;
        }

        if (_isBorderlessMode)
        {
            if (_isManualMaximized)
            {
                ManualRestore();
            }
            else
            {
                ManualMaximize();
            }
        }
        else if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            if (presenter.State == OverlappedPresenterState.Maximized)
            {
                presenter.Restore();
            }
            else
            {
                presenter.Maximize();
            }
        }

        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        if (Application.Current?.Windows.Count > 0)
        {
            Application.Current.CloseWindow(Application.Current.Windows[0]);
        }

        return Task.CompletedTask;
    }

    public void SetSize(int width, int height)
    {
        _appWindow?.Resize(new SizeInt32(width, height));
    }

    public void CenterOnScreen()
    {
        if (_appWindow == null)
        {
            return;
        }

        var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
        if (displayArea == null)
        {
            return;
        }

        var centerX = (displayArea.WorkArea.Width - _appWindow.Size.Width) / 2;
        var centerY = (displayArea.WorkArea.Height - _appWindow.Size.Height) / 2;
        _appWindow.Move(new PointInt32(centerX, centerY));
    }

    public void SetAlwaysOnTop(bool alwaysOnTop)
    {
        if (_appWindow?.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = alwaysOnTop;
        }
    }

    public Task SetApplicationRestartState(bool restart)
    {
        if (_windowHandle != IntPtr.Zero && restart)
        {
            Debug.WriteLine("[WindowService] 应用已标记为更新后重启");
        }

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

    private void ManualMaximize()
    {
        if (_appWindow == null)
        {
            return;
        }

        try
        {
            _restorePosition = _appWindow.Position;
            _restoreSize = _appWindow.Size;

            var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
            if (displayArea == null)
            {
                return;
            }

            _appWindow.Move(new PointInt32(displayArea.WorkArea.X, displayArea.WorkArea.Y));
            _appWindow.Resize(new SizeInt32(displayArea.WorkArea.Width, displayArea.WorkArea.Height));

            _isManualMaximized = true;
            _isMaximized = true;
            OnMaximizedChanged?.Invoke(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ManualMaximize failed: {ex.Message}");
        }
    }

    private void ManualRestore()
    {
        if (_appWindow == null)
        {
            return;
        }

        try
        {
            _appWindow.Move(_restorePosition);
            _appWindow.Resize(_restoreSize);

            _isManualMaximized = false;
            _isMaximized = false;
            OnMaximizedChanged?.Invoke(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ManualRestore failed: {ex.Message}");
        }
    }
}
#endif
