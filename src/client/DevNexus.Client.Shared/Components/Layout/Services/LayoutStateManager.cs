using System;
using System.Diagnostics;

namespace DevNexus.Client.Shared.Components.Layout.Services;

/// <summary>
/// 布局状态管理服务 - 管理布局级别的本地状态
/// </summary>
public class LayoutStateManager
{
    private bool _isMaximized;
    private bool _isListSidebarRouteVisible = true;
    private bool _isListSidebarExpanded = true;
    private int _latency = 0;
    private int _errorBoundaryKey = 0;

    public event Action? OnStateChanged;

    public bool IsMaximized
    {
        get => _isMaximized;
        set
        {
            if (_isMaximized != value)
            {
                _isMaximized = value;
                OnStateChanged?.Invoke();
            }
        }
    }

    public bool IsListSidebarVisible
    {
        get => _isListSidebarRouteVisible;
        set
        {
            if (_isListSidebarRouteVisible != value)
            {
                _isListSidebarRouteVisible = value;
                OnStateChanged?.Invoke();
            }
        }
    }

    /// <summary>
    /// 当前路由允许显示会话侧栏时，用户是否保持展开状态。
    /// </summary>
    public bool IsListSidebarExpanded
    {
        get => _isListSidebarExpanded;
        set
        {
            if (_isListSidebarExpanded != value)
            {
                _isListSidebarExpanded = value;
                OnStateChanged?.Invoke();
            }
        }
    }

    public int Latency
    {
        get => _latency;
        set
        {
            if (_latency != value)
            {
                _latency = value;
                OnStateChanged?.Invoke();
            }
        }
    }

    public int ErrorBoundaryKey => _errorBoundaryKey;

    /// <summary>
    /// 更新侧边栏可见性（根据当前路由）
    /// 仅在聊天页面（/ 或 /chat/*）显示会话列表侧边栏
    /// </summary>
    public void UpdateSidebarVisibility(string currentUri)
    {
        try
        {
            var uri = new Uri(currentUri);
            var path = uri.AbsolutePath.ToLower();
            var isChatPage = path == "/" || path.StartsWith("/chat");
            IsListSidebarVisible = isChatPage;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LayoutStateManager] URI 解析失败: {ex.Message}");
            IsListSidebarVisible = true; // 默认显示
        }
    }

    /// <summary>
    /// 切换会话侧栏展开状态。
    /// </summary>
    public void ToggleListSidebarExpanded()
    {
        IsListSidebarExpanded = !IsListSidebarExpanded;
    }

    /// <summary>
    /// 重置 ErrorBoundary Key 以重新渲染错误边界
    /// </summary>
    public void ResetErrorBoundary()
    {
        _errorBoundaryKey++;
        OnStateChanged?.Invoke();
    }
}
