namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// 窗口服务接口 - 提供跨平台窗口控制能力
/// </summary>
public interface IWindowService
{
    #region 基础窗口操作
    Task MinimizeAsync();
    Task MaximizeAsync();
    Task ToggleMaximizeAsync();
    Task RestoreAsync();
    Task CloseAsync();
    bool IsMaximized { get; }
    event Action<bool>? OnMaximizedChanged;
    #endregion

    #region 无边框窗口
    void HideTitleBar();
    void EnableBorderlessMode();
    bool IsTitleBarHidden { get; }
    bool IsBorderlessMode { get; }
    #endregion

    #region 窗口拖拽
    void StartDrag();
    void SetDragRegion(int titleBarHeight);
    #endregion

    #region 窗口设置
    void SetSize(int width, int height);
    void CenterOnScreen();
    void SetAlwaysOnTop(bool alwaysOnTop);
    #endregion

    #region 屏幕信息
    ScreenInfo GetScreenInfo();
    void SetAdaptiveSize(string windowType);
    #endregion

    #region 独立窗口
    void OpenWebWindow(string url, string title);
    void OpenArtifactWindow(DevNexus.Shared.DTOs.ArtifactDto artifact);
    void OpenSwarmWindow(Guid sessionId);
    void OpenAuditWindow(Guid userId, string displayName);
    #endregion

    #region 更新支持
    Task SetApplicationRestartState(bool restart);
    void CloseApplication();
    #endregion
}

/// <summary>
/// 屏幕信息
/// </summary>
public record ScreenInfo
{
    public int Width { get; init; }
    public int Height { get; init; }
    public int WorkAreaWidth { get; init; }
    public int WorkAreaHeight { get; init; }
    public double ScaleFactor { get; init; }
    public string ScreenType { get; init; } = "HD";

    public static string DetermineScreenType(int width, int height)
    {
        if (height >= 2160 || width >= 3840)
            return "4K";
        if (height >= 1440 || width >= 2560)
            return "2K";
        return "HD";
    }
}

