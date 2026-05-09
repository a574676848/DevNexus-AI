using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DevNexus.Core.Services.Swarm.Context;

/// <summary>
/// 项目全局上下文接口 - 存储任务执行过程中产生的关键事实、决策和标志位
/// </summary>
public interface IProjectContext
{
    void SetValue(string key, object value);
    T? GetValue<T>(string key);
    bool HasKey(string key);
    Dictionary<string, object> GetAll();
}

/// <summary>
/// 基于内存的项目上下文实现
/// </summary>
public class InMemoryProjectContext : IProjectContext
{
    private readonly ConcurrentDictionary<string, object> _data = new();

    public void SetValue(string key, object value) => _data[key] = value;

    public T? GetValue<T>(string key)
    {
        if (_data.TryGetValue(key, out var val) && val is T typedVal)
            return typedVal;
        return default;
    }

    public bool HasKey(string key) => _data.ContainsKey(key);

    public Dictionary<string, object> GetAll() => new(_data);
}
