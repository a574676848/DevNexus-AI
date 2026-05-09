using Microsoft.JSInterop;
using DevNexus.Client.Shared.Abstractions;

namespace DevNexus.Client.Shared.Services.UI;

/// <summary>
/// 主题服务实现 - 管理明暗主题切换
/// </summary>
public class ThemeService : IThemeService
{
    private readonly IJSRuntime _jsRuntime;
    private string _currentTheme = "dark";
    private const string ThemeStorageKey = "devnexus_theme";

    /// <inheritdoc />
    public string CurrentTheme => _currentTheme;

    /// <inheritdoc />
    public bool IsDarkTheme => _currentTheme == "dark";

    /// <inheritdoc />
    public event Action<string>? OnThemeChanged;

    public ThemeService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        try
        {
            // 从本地存储加载主题偏好
            var savedTheme = await _jsRuntime.InvokeAsync<string?>(
                "localStorage.getItem", ThemeStorageKey);

            if (!string.IsNullOrEmpty(savedTheme) && (savedTheme == "dark" || savedTheme == "light"))
            {
                _currentTheme = savedTheme;
            }
            else
            {
                // 检查系统偏好
                var prefersDark = await _jsRuntime.InvokeAsync<bool>(
                    "eval", "window.matchMedia('(prefers-color-scheme: dark)').matches");
                _currentTheme = prefersDark ? "dark" : "light";
            }

            await ApplyThemeAsync();
        }
        catch
        {
            // 初始化失败时使用默认暗色主题
            _currentTheme = "dark";
        }
    }

    /// <inheritdoc />
    public async Task SetThemeAsync(string theme)
    {
        if (theme != "dark" && theme != "light")
        {
            throw new ArgumentException("Theme must be 'dark' or 'light'", nameof(theme));
        }

        if (_currentTheme == theme) return;

        _currentTheme = theme;
        await ApplyThemeAsync();
        await SaveThemeAsync();

        OnThemeChanged?.Invoke(_currentTheme);
    }

    /// <inheritdoc />
    public async Task ToggleThemeAsync()
    {
        var newTheme = _currentTheme == "dark" ? "light" : "dark";
        await SetThemeAsync(newTheme);
    }

    /// <summary>
    /// 应用主题到 DOM
    /// </summary>
    private async Task ApplyThemeAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync(
                "eval", $"document.documentElement.setAttribute('data-theme', '{_currentTheme}')");
        }
        catch
        {
            // 忽略 JS 调用失败
        }
    }

    /// <summary>
    /// 保存主题到本地存储
    /// </summary>
    private async Task SaveThemeAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync(
                "localStorage.setItem", ThemeStorageKey, _currentTheme);
        }
        catch
        {
            // 忽略存储失败
        }
    }
}
