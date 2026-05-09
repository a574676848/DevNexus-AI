namespace DevNexus.Client.Services.Platform;

/// <summary>
/// 允许平台窗口服务在窗口创建后拿到原生宿主对象。
/// </summary>
internal interface IWindowLifecycleHandler
{
    void Initialize(Microsoft.Maui.Controls.Window window);
}
