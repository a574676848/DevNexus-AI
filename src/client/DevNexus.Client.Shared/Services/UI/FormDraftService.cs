namespace DevNexus.Client.Shared.Services.UI;

/// <summary>
/// 表单草稿自动保存服务
/// 使用内存存储，不依赖外部库
/// </summary>
public class FormDraftService : IDisposable
{
    private readonly Dictionary<string, string> _drafts = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<string, DebouncedSave> _pendingSaves = new();
    
    /// <summary>
    /// 保存草稿（自动防抖）
    /// </summary>
    public async Task SaveDraftAsync(string key, string content, int delayMs = 1000)
    {
        await _lock.WaitAsync();
        try
        {
            if (_pendingSaves.TryGetValue(key, out var existingSave))
            {
                existingSave.Cancel();
                _pendingSaves.Remove(key);
            }
            
            var newSave = new DebouncedSave(() =>
            {
                _drafts[$"draft:{key}"] = content;
                _pendingSaves.Remove(key);
                return Task.CompletedTask;
            }, delayMs);
            
            _pendingSaves[key] = newSave;
            newSave.Start();
        }
        finally
        {
            _lock.Release();
        }
    }
    
    /// <summary>
    /// 获取草稿
    /// </summary>
    public Task<string?> GetDraftAsync(string key)
    {
        _drafts.TryGetValue($"draft:{key}", out var content);
        return Task.FromResult(content);
    }
    
    /// <summary>
    /// 清除草稿
    /// </summary>
    public Task ClearDraftAsync(string key)
    {
        _drafts.Remove($"draft:{key}");
        
        if (_pendingSaves.TryGetValue(key, out var save))
        {
            save.Cancel();
            _pendingSaves.Remove(key);
        }
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// 清除所有草稿
    /// </summary>
    public Task ClearAllDraftsAsync()
    {
        foreach (var key in _pendingSaves.Keys.ToList())
        {
            _pendingSaves[key].Cancel();
        }
        
        _pendingSaves.Clear();
        _drafts.Clear();
        
        return Task.CompletedTask;
    }
    
    public void Dispose()
    {
        _lock.Dispose();
        
        foreach (var save in _pendingSaves.Values)
        {
            save.Cancel();
        }
    }
    
    private class DebouncedSave
    {
        private readonly Func<Task> _action;
        private readonly int _delayMs;
        private CancellationTokenSource? _cts;
        
        public DebouncedSave(Func<Task> action, int delayMs)
        {
            _action = action;
            _delayMs = delayMs;
        }
        
        public void Start()
        {
            _cts = new CancellationTokenSource();
            _ = RunAsync(_cts.Token);
        }
        
        public void Cancel()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
        
        private async Task RunAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(_delayMs, token);
                if (!token.IsCancellationRequested)
                {
                    await _action();
                }
            }
            catch (OperationCanceledException)
            {
                // 忽略取消
            }
        }
    }
}
