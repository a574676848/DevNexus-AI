#if WINDOWS
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;

namespace DevNexus.Client.Services.Platform;

/// <summary>
/// Windows 平台窗口服务实现
/// 提供无边框窗口、窗口控制、窗口拖拽等功能
/// </summary>
public partial class WindowService : IWindowService, IWindowLifecycleHandler
{
    private AppWindow? _appWindow;
    private Microsoft.UI.Xaml.Window? _nativeWindow;
    private IntPtr _windowHandle;
    private bool _isMaximized;
    private bool _isTitleBarHidden;
    private bool _isBorderlessMode;
    private InputNonClientPointerSource? _nonClientInputSrc;
    private PointInt32 _restorePosition;
    private SizeInt32 _restoreSize;
    private bool _isManualMaximized;

    public bool IsMaximized => _isMaximized;
    public bool IsTitleBarHidden => _isTitleBarHidden;
    public bool IsBorderlessMode => _isBorderlessMode;
    public event Action<bool>? OnMaximizedChanged;

    /// <summary>
    /// 初始化 Windows 窗口服务
    /// </summary>
    public void Initialize(Microsoft.Maui.Controls.Window window)
    {
        _nativeWindow = window.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
        if (_nativeWindow == null)
        {
            return;
        }

        _windowHandle = WindowNative.GetWindowHandle(_nativeWindow);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_windowHandle);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow == null)
        {
            return;
        }

        _appWindow.Changed += OnAppWindowChanged;
        _nonClientInputSrc = InputNonClientPointerSource.GetForWindowId(_appWindow.Id);
        UpdateMaximizedState();
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if ((args.DidPresenterChange || args.DidPositionChange || args.DidSizeChange) && !_isManualMaximized)
        {
            UpdateMaximizedState();
        }
    }

    private void UpdateMaximizedState()
    {
        if (_appWindow?.Presenter is not OverlappedPresenter presenter)
        {
            return;
        }

        var wasMaximized = _isMaximized;
        _isMaximized = _isManualMaximized || presenter.State == OverlappedPresenterState.Maximized;

        if (wasMaximized != _isMaximized)
        {
            OnMaximizedChanged?.Invoke(_isMaximized);
        }
    }
}
#endif
