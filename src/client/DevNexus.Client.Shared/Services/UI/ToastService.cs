namespace DevNexus.Client.Shared.Services.UI;

/// <summary>
/// Toast 通知类型
/// </summary>
public enum ToastType
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// Toast 通知选项
/// </summary>
public class ToastOptions
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Message { get; set; } = string.Empty;
    public string? Title { get; set; }
    public ToastType Type { get; set; } = ToastType.Info;
    public int Duration { get; set; } = 3000;
    public bool ShowClose { get; set; } = true;
    public bool IsClosing { get; set; }
    public Action? OnClose { get; set; }
    public Action? OnClick { get; set; }
}

/// <summary>
/// Toast 通知服务
/// 提供全局统一的通知提示功能
/// </summary>
public class ToastService : IDisposable
{
    private readonly List<ToastOptions> _toasts = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    /// <summary>
    /// 当新增 Toast 时触发
    /// </summary>
    public event Action<ToastOptions>? OnToastAdded;
    
    /// <summary>
    /// 当移除 Toast 时触发
    /// </summary>
    public event Action<string>? OnToastRemoved;
    
    /// <summary>
    /// 当所有 Toast 被清除时触发
    /// </summary>
    public event Action? OnToastsCleared;
    
    /// <summary>
    /// 获取当前活动的 Toast 列表
    /// </summary>
    public IReadOnlyList<ToastOptions> Toasts
    {
        get
        {
            lock (_toasts)
            {
                return _toasts.AsReadOnly();
            }
        }
    }
    
    /// <summary>
    /// 显示信息提示
    /// </summary>
    public void Show(string message, ToastType type = ToastType.Info, int duration = 3000)
    {
        var toast = new ToastOptions
        {
            Message = message,
            Type = type,
            Duration = duration
        };
        
        AddToast(toast);
    }
    
    /// <summary>
    /// 显示成功提示
    /// </summary>
    public void Success(string message, int duration = 3000)
    {
        Show(message, ToastType.Success, duration);
    }
    
    /// <summary>
    /// 显示错误提示
    /// </summary>
    public void Error(string message, int duration = 5000)
    {
        Show(message, ToastType.Error, duration);
    }
    
    /// <summary>
    /// 显示警告提示
    /// </summary>
    public void Warning(string message, int duration = 4000)
    {
        Show(message, ToastType.Warning, duration);
    }
    
    /// <summary>
    /// 显示带标题的 Toast
    /// </summary>
    public void ShowWithTitle(string title, string message, ToastType type = ToastType.Info, int duration = 3000)
    {
        var toast = new ToastOptions
        {
            Title = title,
            Message = message,
            Type = type,
            Duration = duration
        };
        
        AddToast(toast);
    }
    
    /// <summary>
    /// 显示可点击的 Toast
    /// </summary>
    public void ShowClickable(string message, Action onClick, string closeText = "关闭", int duration = 5000)
    {
        var toast = new ToastOptions
        {
            Message = message,
            Duration = duration,
            ShowClose = true,
            OnClick = onClick,
            OnClose = () => closeText = "关闭"
        };
        
        AddToast(toast);
    }
    
    /// <summary>
    /// 移除指定 Toast
    /// </summary>
    public void Remove(string toastId)
    {
        lock (_toasts)
        {
            var toast = _toasts.FirstOrDefault(t => t.Id == toastId);
            if (toast != null)
            {
                _toasts.Remove(toast);
                OnToastRemoved?.Invoke(toastId);
            }
        }
    }
    
    /// <summary>
    /// 清除所有 Toast
    /// </summary>
    public void Clear()
    {
        List<string> ids;
        lock (_toasts)
        {
            ids = _toasts.Select(t => t.Id).ToList();
            _toasts.Clear();
        }
        
        foreach (var id in ids)
        {
            OnToastRemoved?.Invoke(id);
        }
        
        OnToastsCleared?.Invoke();
    }
    
    /// <summary>
    /// 添加 Toast（内部方法）
    /// </summary>
    private void AddToast(ToastOptions toast)
    {
        lock (_toasts)
        {
            _toasts.Add(toast);
        }
        
        OnToastAdded?.Invoke(toast);
        
        // 如果有时长，自动移除
        if (toast.Duration > 0)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(toast.Duration);
                Remove(toast.Id);
            });
        }
    }
    
    public void Dispose()
    {
        _lock.Dispose();
    }
}
