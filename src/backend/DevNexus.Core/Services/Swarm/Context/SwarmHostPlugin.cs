using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using DevNexus.Core.Abstractions;
using DevNexus.Core.Models.Execution;
using Microsoft.SemanticKernel;

namespace DevNexus.Core.Services.Swarm.Context;

/// <summary>
/// 增强型的 Swarm 主机插件 - 在执行物理文件操作前优先检索虚拟文件系统 (VFS)
/// </summary>
public class SwarmHostPlugin
{
    private readonly IHostStructuredService _physicalHost;
    private readonly IBlackboard _blackboard;
    private readonly string _currentTaskId;

    public SwarmHostPlugin(IHostStructuredService physicalHost, IBlackboard blackboard, string taskId)
    {
        _physicalHost = physicalHost;
        _blackboard = blackboard;
        _currentTaskId = taskId;
    }

    [KernelFunction, Description("Reads the content of a text file (preferring VFS).")]
    public async Task<string> ReadFileTextAsync(
        [Description("The absolute path of the file")] string path, 
        CancellationToken cancellationToken = default)
    {
        // 1. 尝试从 VFS 获取
        var vfsContent = _blackboard.Vfs.GetContent(path);
        if (vfsContent != null)
        {
            return vfsContent;
        }

        // 2. 否则从物理磁盘获取
        return HostOperationTextFormatter.FormatText(
            await _physicalHost.ReadFileTextResultAsync(path, cancellationToken));
    }

    [KernelFunction, Description("Writes content to a text file (updates VFS and physical disk).")]
    public async Task<string> WriteFileTextAsync(
        [Description("The absolute path of the file")] string path, 
        [Description("The text content to write")] string content, 
        CancellationToken cancellationToken = default)
    {
        // 1. 更新 VFS
        _blackboard.Vfs.TrackChange(path, content, _currentTaskId);

        // 2. 写入物理磁盘
        return HostOperationTextFormatter.Format(
            await _physicalHost.WriteFileTextResultAsync(path, content, cancellationToken));
    }

    [KernelFunction, Description("Lists files recursively matching the patterns.")]
    public async Task<object> ListFilesRecursiveAsync(
        [Description("The root directory path")] string path, 
        [Description("Search patterns (e.g., *.cs, *.md)")] string[] patterns,
        CancellationToken cancellationToken = default)
    {
        // 目前 List 直接透传给物理系统
        return HostOperationTextFormatter.FormatFileList(
            await _physicalHost.ListFilesRecursiveResultAsync(path, patterns, cancellationToken));
    }

    [KernelFunction, Description("Executes a shell command in the specified working directory.")]
    public async Task<string> ExecuteCommandAsync(
        [Description("The command to execute (e.g., git, dotnet)")] string command, 
        [Description("The arguments for the command")] string arguments, 
        [Description("The working directory path")] string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        return HostOperationTextFormatter.FormatCommand(
            await _physicalHost.ExecuteCommandResultAsync(command, arguments, workingDirectory, cancellationToken));
    }
}
