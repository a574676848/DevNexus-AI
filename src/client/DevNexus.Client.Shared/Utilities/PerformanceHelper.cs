using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components;

namespace DevNexus.Client.Shared.Utilities;

/// <summary>
/// 性能优化助手类
/// 提供常用的性能优化工具方法
/// </summary>
public static class PerformanceHelper
{
    #region 防抖与节流

    /// <summary>
    /// 防抖（Debounce）：在事件触发后延迟执行，如果在延迟期间再次触发则重新计时
    /// 适用场景：搜索框输入、窗口 resize 等
    /// </summary>
    public static Action<T> Debounce<T>(Action<T> action, TimeSpan delay)
    {
        CancellationTokenSource? cts = null;

        return arg =>
        {
            cts?.Cancel();
            cts = new CancellationTokenSource();

            Task.Delay(delay, cts.Token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                {
                    action(arg);
                }
            }, TaskScheduler.Default);
        };
    }

    /// <summary>
    /// 防抖（异步版本）
    /// 适用场景：异步搜索、API 调用等
    /// </summary>
    public static Func<T, Task> DebounceAsync<T>(Func<T, Task> action, TimeSpan delay)
    {
        CancellationTokenSource? cts = null;

        return async arg =>
        {
            cts?.Cancel();
            cts = new CancellationTokenSource();

            try
            {
                await Task.Delay(delay, cts.Token);
                if (!cts.Token.IsCancellationRequested)
                {
                    await action(arg);
                }
            }
            catch (TaskCanceledException)
            {
                // 预期中的取消，忽略
            }
        };
    }

    /// <summary>
    /// 节流（Throttle）：在指定时间间隔内只执行一次
    /// 适用场景：滚动事件、按钮连点等
    /// </summary>
    public static Action<T> Throttle<T>(Action<T> action, TimeSpan interval)
    {
        var lastRun = DateTime.MinValue;
        var sync = new object();

        return arg =>
        {
            lock (sync)
            {
                var now = DateTime.UtcNow;
                if (now - lastRun >= interval)
                {
                    action(arg);
                    lastRun = now;
                }
            }
        };
    }

    /// <summary>
    /// 节流（无参数版本）：在指定时间间隔内只执行一次
    /// 适用场景：无参数的操作节流
    /// </summary>
    public static Action Throttle(Action action, TimeSpan interval)
    {
        var lastRun = DateTime.MinValue;
        var sync = new object();

        return () =>
        {
            lock (sync)
            {
                var now = DateTime.UtcNow;
                if (now - lastRun >= interval)
                {
                    action();
                    lastRun = now;
                }
            }
        };
    }

    /// <summary>
    /// 节流（异步版本）
    /// 适用场景：异步操作的节流控制
    /// </summary>
    public static Func<T, Task> ThrottleAsync<T>(Func<T, Task> action, TimeSpan interval)
    {
        var lastRun = DateTime.MinValue;
        var sync = new object();
        Task? pendingTask = null;

        return async arg =>
        {
            Task taskToAwait;

            lock (sync)
            {
                var now = DateTime.UtcNow;
                if (now - lastRun >= interval)
                {
                    lastRun = now;
                    pendingTask = action(arg);
                    taskToAwait = pendingTask;
                }
                else
                {
                    taskToAwait = pendingTask ?? Task.CompletedTask;
                }
            }

            await taskToAwait;
        };
    }

    /// <summary>
    /// Blazor 组件专用的节流渲染助手
    /// 适用场景：流式内容渲染、频繁状态更新
    /// </summary>
    /// <param name="componentBase">Blazor 组件实例</param>
    /// <param name="stateUpdate">状态更新操作</param>
    /// <param name="intervalMs">节流间隔（毫秒）</param>
    /// <returns>节流后的操作</returns>
    public static Action CreateThrottledRenderForBlazor(
        ComponentBase componentBase,
        Action stateUpdate,
        int intervalMs = 50)
    {
        var lastRun = DateTime.MinValue;
        var sync = new object();
        var pending = false;

        return () =>
        {
            var now = DateTime.UtcNow;

            lock (sync)
            {
                if ((now - lastRun).TotalMilliseconds >= intervalMs)
                {
                    lastRun = now;
                    if (!pending)
                    {
                        pending = true;
                        // 使用 Task.Run 在后台执行，然后通过 InvokeAsync 更新 UI
                        _ = Task.Run(async () =>
                        {
                            await Task.Delay(10); // 让出 UI 线程

                            // 使用公开的 Dispatcher.InvokeAsync 替换受保护的 ComponentBase.InvokeAsync
                            // 这是从外部触发 Blazor 组件 UI 线程操作的正确方式
                            await InvokeAsyncOnComponent(componentBase, () =>
                            {
                                stateUpdate();
                                pending = false;
                                return Task.CompletedTask;
                            });
                        });
                    }
                }
            }
        };
    }

    /// <summary>
    /// 从外部调用组件的 InvokeAsync (解决 ComponentBase.InvokeAsync 受保护不可直接访问的问题)
    /// </summary>
    private static Task InvokeAsyncOnComponent(ComponentBase component, Func<Task> workItem)
    {
        // 尝试通过反射获取组件的 Dispatcher
        var dispatcherProperty = typeof(ComponentBase).GetProperty("Dispatcher", 
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        
        var dispatcher = dispatcherProperty?.GetValue(component) as Microsoft.AspNetCore.Components.Dispatcher;

        if (dispatcher != null)
        {
            return dispatcher.InvokeAsync(workItem);
        }

        // 如果获取失败（理论上不应该），退而求其次在后台执行并同步状态
        return Task.Run(workItem);
    }

    #endregion

    #region 批处理

    /// <summary>
    /// 批处理器：将多个操作批量处理以提高性能
    /// 适用场景：SignalR 消息批处理、DOM 批量更新等
    /// </summary>
    public class BatchProcessor<T>
    {
        private readonly Action<List<T>> _batchAction;
        private readonly int _batchSize;
        private readonly TimeSpan _batchDelay;
        private readonly List<T> _buffer = new();
        private readonly object _lock = new();
        private CancellationTokenSource? _cts;

        public BatchProcessor(
            Action<List<T>> batchAction,
            int batchSize = 10,
            TimeSpan? batchDelay = null)
        {
            _batchAction = batchAction;
            _batchSize = batchSize;
            _batchDelay = batchDelay ?? TimeSpan.FromMilliseconds(50);
        }

        public void Add(T item)
        {
            List<T>? itemsToProcess = null;

            lock (_lock)
            {
                _buffer.Add(item);

                // 达到批处理大小立即执行
                if (_buffer.Count >= _batchSize)
                {
                    itemsToProcess = new List<T>(_buffer);
                    _buffer.Clear();
                    _cts?.Cancel();
                    _cts = null;
                }
                // 启动延迟批处理
                else if (_cts == null)
                {
                    _cts = new CancellationTokenSource();
                    var token = _cts.Token;

                    Task.Delay(_batchDelay, token).ContinueWith(_ =>
                    {
                        if (!token.IsCancellationRequested)
                        {
                            Flush();
                        }
                    }, TaskScheduler.Default);
                }
            }

            if (itemsToProcess != null)
            {
                _batchAction(itemsToProcess);
            }
        }

        public void Flush()
        {
            List<T>? itemsToProcess = null;

            lock (_lock)
            {
                if (_buffer.Count > 0)
                {
                    itemsToProcess = new List<T>(_buffer);
                    _buffer.Clear();
                }
                _cts?.Cancel();
                _cts = null;
            }

            if (itemsToProcess != null)
            {
                _batchAction(itemsToProcess);
            }
        }
    }

    #endregion

    #region 对象池

    /// <summary>
    /// 简单对象池：减少 GC 压力
    /// 适用场景：频繁创建销毁的临时对象
    /// </summary>
    public class ObjectPool<T> where T : class, new()
    {
        private readonly ConcurrentBag<T> _objects = new();
        private readonly int _maxSize;

        public ObjectPool(int maxSize = 100)
        {
            _maxSize = maxSize;
        }

        public T Rent()
        {
            return _objects.TryTake(out var obj) ? obj : new T();
        }

        public void Return(T obj)
        {
            if (_objects.Count < _maxSize)
            {
                _objects.Add(obj);
            }
        }
    }

    #endregion

    #region 内存优化

    /// <summary>
    /// 强制垃圾回收（谨慎使用）
    /// 仅在大量内存释放后手动触发
    /// </summary>
    public static void ForceGarbageCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    /// <summary>
    /// 获取当前内存使用（MB）
    /// </summary>
    public static long GetMemoryUsageMB()
    {
        return GC.GetTotalMemory(false) / (1024 * 1024);
    }

    /// <summary>
    /// 清理大对象堆（LOH）
    /// </summary>
    public static void CompactLargeObjectHeap()
    {
        System.Runtime.GCSettings.LargeObjectHeapCompactionMode = 
            System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect();
    }

    #endregion

    #region 异步优化

    /// <summary>
    /// 并发执行多个任务并限制并发数
    /// </summary>
    public static async Task ForEachAsync<T>(
        IEnumerable<T> source,
        int maxDegreeOfParallelism,
        Func<T, Task> action)
    {
        using var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
        var tasks = source.Select(async item =>
        {
            await semaphore.WaitAsync();
            try
            {
                await action(item);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// 带超时的异步操作
    /// </summary>
    public static async Task<T> WithTimeout<T>(
        Task<T> task,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var delayTask = Task.Delay(timeout, cts.Token);

        var completedTask = await Task.WhenAny(task, delayTask);

        if (completedTask == delayTask)
        {
            throw new TimeoutException($"操作超时 ({timeout.TotalSeconds}秒)");
        }

        cts.Cancel(); // 取消延迟任务
        return await task;
    }

    #endregion

    #region 缓存助手

    /// <summary>
    /// 简单的内存缓存（带过期时间）
    /// </summary>
    public class MemoryCache<TKey, TValue> where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, CacheEntry> _cache = new();
        private readonly TimeSpan _defaultExpiration;

        public MemoryCache(TimeSpan? defaultExpiration = null)
        {
            _defaultExpiration = defaultExpiration ?? TimeSpan.FromMinutes(5);
        }

        public void Set(TKey key, TValue value, TimeSpan? expiration = null)
        {
            var entry = new CacheEntry
            {
                Value = value,
                ExpiresAt = DateTime.UtcNow + (expiration ?? _defaultExpiration)
            };
            _cache[key] = entry;
        }

        public bool TryGet(TKey key, out TValue? value)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.ExpiresAt > DateTime.UtcNow)
                {
                    value = entry.Value;
                    return true;
                }
                else
                {
                    // 过期，移除
                    _cache.TryRemove(key, out _);
                }
            }

            value = default;
            return false;
        }

        public void Clear()
        {
            _cache.Clear();
        }

        private class CacheEntry
        {
            public TValue Value { get; set; } = default!;
            public DateTime ExpiresAt { get; set; }
        }
    }

    #endregion

    #region 字符串优化

    /// <summary>
    /// 高性能字符串拼接（使用 StringBuilder）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string FastConcat(params string[] strings)
    {
        if (strings.Length == 0) return string.Empty;
        if (strings.Length == 1) return strings[0];
        
        var totalLength = 0;
        foreach (var str in strings)
        {
            totalLength += str?.Length ?? 0;
        }

        return string.Create(totalLength, strings, (buffer, state) =>
        {
            var position = 0;
            foreach (var str in state)
            {
                if (str != null)
                {
                    str.AsSpan().CopyTo(buffer.Slice(position));
                    position += str.Length;
                }
            }
        });
    }

    #endregion
}
