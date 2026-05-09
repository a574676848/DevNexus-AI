using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DevNexus.Core.Services.Swarm.Context;

/// <summary>
/// 黑板模式实现 - 用于任务与任务之间共享状态和上下文
/// </summary>
public interface IBlackboard
{
    /// <summary>
    /// 项目全局上下文
    /// </summary>
    IProjectContext ProjectContext { get; }

    /// <summary>
    /// 虚拟文件系统快照
    /// </summary>
    IVirtualFileSystem Vfs { get; }

    /// <summary>
    /// 接口注册表
    /// </summary>
    IInterfaceRegistry InterfaceRegistry { get; }

    /// <summary>
    /// 获取特定任务的输出
    /// </summary>
    string? GetTaskOutput(string taskId);
    
    /// <summary>
    /// 存储任务输出
    /// </summary>
    void SetTaskOutput(string taskId, string content);

    /// <summary>
    /// 获取所有指定任务的输出集合
    /// </summary>
    Dictionary<string, string> GetOutputs(List<string> taskIds);
}

/// <summary>
/// 内存中的简单黑板实现
/// </summary>
public class InMemoryBlackboard : IBlackboard
{
    private readonly ConcurrentDictionary<string, string> _outputs = new();
    
    public IProjectContext ProjectContext { get; } = new InMemoryProjectContext();
    public IVirtualFileSystem Vfs { get; } = new InMemoryVirtualFileSystem();
    public IInterfaceRegistry InterfaceRegistry { get; } = new InMemoryInterfaceRegistry();

    public string? GetTaskOutput(string taskId)
    {
        return _outputs.TryGetValue(taskId, out var val) ? val : null;
    }

    public void SetTaskOutput(string taskId, string content)
    {
        if (content == null) return;
        _outputs[taskId] = content;
    }

    public Dictionary<string, string> GetOutputs(List<string> taskIds)
    {
        var result = new Dictionary<string, string>();
        foreach (var id in taskIds)
        {
            if (_outputs.TryGetValue(id, out var val))
            {
                result[id] = val;
            }
        }
        return result;
    }
}
