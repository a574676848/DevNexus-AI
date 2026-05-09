using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DevNexus.Core.Services.Swarm.Context;

/// <summary>
/// 虚拟文件系统快照 - 追踪任务执行过程中对文件的修改
/// </summary>
public interface IVirtualFileSystem
{
    void TrackChange(string path, string content, string taskId);
    string? GetContent(string path);
    Dictionary<string, (string Content, string TaskId)> GetModifiedFiles();
}

/// <summary>
/// 内存中的虚拟文件系统快照实现
/// </summary>
public class InMemoryVirtualFileSystem : IVirtualFileSystem
{
    private readonly ConcurrentDictionary<string, (string Content, string TaskId)> _files = new();

    public void TrackChange(string path, string content, string taskId)
    {
        _files[path] = (content, taskId);
    }

    public string? GetContent(string path)
    {
        return _files.TryGetValue(path, out var val) ? val.Content : null;
    }

    public Dictionary<string, (string Content, string TaskId)> GetModifiedFiles()
    {
        return new(_files);
    }
}
