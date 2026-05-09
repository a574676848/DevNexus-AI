using DevNexus.Client.Services.Platform;
using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace DevNexus.Client;

public partial class App : Application
{
	private readonly IWindowService _windowService;

	public App(IWindowService windowService)
	{
		_windowService = windowService;
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new MainPage()) 
		{ 
			// Title 留空，防止系统显示标题栏文字
			Title = "",
			// 提供一个安全初值，窗口创建完成后会立即切换到桌面主界面的最大化状态
			Width = 1100,
			Height = 750,
			MinimumWidth = 320,
			MinimumHeight = 480
		};

		window.Created += (s, e) =>
		{
			// 使用 Dispatcher 延后执行，确保 MAUI 初始化完成
			window.Dispatcher.Dispatch(() => 
			{
				InitializeWindowFeatures(window);
			});
		};

		return window;
	}

	private void InitializeWindowFeatures(Window window)
	{
		try
		{
			if (_windowService is IWindowLifecycleHandler lifecycleHandler)
			{
				lifecycleHandler.Initialize(window);
			}

			_windowService.EnableBorderlessMode();
			// 启动恢复登录态期间应与首页保持一致，先最大化；真正进入登录页后再缩回登录窗尺寸。
			_ = _windowService.MaximizeAsync();

			window.SizeChanged += (s, e) =>
			{
				_windowService.SetDragRegion(32);
			};
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"初始化窗口功能失败: {ex.Message}");
		}
	}
}
