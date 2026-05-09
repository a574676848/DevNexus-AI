namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// 主题服务接口 - 提供明暗主题切换能力
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// 当前主题 (dark/light)
    /// </summary>
    string CurrentTheme { get; }

    /// <summary>
    /// 是否为暗色主题
    /// </summary>
    bool IsDarkTheme { get; }

    /// <summary>
    /// 主题变更事件
    /// </summary>
    event Action<string>? OnThemeChanged;

    /// <summary>
    /// 初始化主题 (从存储加载)
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// 设置主题
    /// </summary>
    Task SetThemeAsync(string theme);

    /// <summary>
    /// 切换主题 (明暗切换)
    /// </summary>
    Task ToggleThemeAsync();
}

