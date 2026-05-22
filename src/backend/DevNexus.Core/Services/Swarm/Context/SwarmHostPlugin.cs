using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using DevNexus.Core.Abstractions;
using DevNexus.Core.Models.Execution;
using DevNexus.Core.Services.Cli;
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
    private readonly ICliRuntimeCoordinator? _cliRuntimeCoordinator;
    private readonly IUserContextAccessor? _userContextAccessor;

    public SwarmHostPlugin(IHostStructuredService physicalHost, IBlackboard blackboard, string taskId)
    {
        _physicalHost = physicalHost;
        _blackboard = blackboard;
        _currentTaskId = taskId;
    }

    public SwarmHostPlugin(
        IHostStructuredService physicalHost,
        IBlackboard blackboard,
        string taskId,
        ICliRuntimeCoordinator cliRuntimeCoordinator,
        IUserContextAccessor userContextAccessor)
        : this(physicalHost, blackboard, taskId)
    {
        _cliRuntimeCoordinator = cliRuntimeCoordinator;
        _userContextAccessor = userContextAccessor;
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

    [KernelFunction, Description("Waits for or polls the current chat CLI session without starting a duplicate command.")]
    public async Task<string> WaitCommandAsync(
        [Description("Wait timeout in milliseconds, clamped to 1000-30000.")] int timeoutMs = CliContinuationWaitBudgetPolicy.DefaultWaitMilliseconds,
        CancellationToken cancellationToken = default)
    {
        var context = ResolveCliContext();
        if (context == null)
        {
            return TaggedExecutionText.Failure("缺少 CLI 会话上下文，无法等待终端命令。");
        }

        var session = await _cliRuntimeCoordinator!.WaitForExitAsync(
            context.Value.UserId,
            context.Value.SessionId,
            CliContinuationWaitBudgetPolicy.Normalize(timeoutMs),
            cancellationToken);

        return CliContinuationToolResponseFormatter.Format("等待终端命令完成", session);
    }

    [KernelFunction, Description("Sends stdin to the current chat CLI session; use an empty string to send a blank line.")]
    public async Task<string> SendCommandInputAsync(
        [Description("Input text to send to the terminal. Runtime protocol handles the trailing newline.")] string input,
        CancellationToken cancellationToken = default)
    {
        var context = ResolveCliContext();
        if (context == null)
        {
            return TaggedExecutionText.Failure("缺少 CLI 会话上下文，无法发送终端输入。");
        }

        var session = await _cliRuntimeCoordinator!.WriteInputAsync(
            context.Value.UserId,
            context.Value.SessionId,
            input,
            cancellationToken);

        return CliContinuationToolResponseFormatter.Format("已发送终端输入", session);
    }

    [KernelFunction, Description("Stops the current chat CLI session when a command is stuck or no longer needed.")]
    public async Task<string> StopCommandAsync(CancellationToken cancellationToken = default)
    {
        var context = ResolveCliContext();
        if (context == null)
        {
            return TaggedExecutionText.Failure("缺少 CLI 会话上下文，无法停止终端命令。");
        }

        var result = await _cliRuntimeCoordinator!.TerminateAsync(
            context.Value.UserId,
            context.Value.SessionId,
            cancellationToken);

        return CliContinuationToolResponseFormatter.FormatTermination("停止终端命令", result);
    }

    private (Guid UserId, Guid SessionId)? ResolveCliContext()
    {
        if (_cliRuntimeCoordinator == null || _userContextAccessor?.CurrentUserId == null)
        {
            return null;
        }

        return Guid.TryParse(_userContextAccessor.CurrentSessionId, out var sessionId)
            ? (_userContextAccessor.CurrentUserId.Value, sessionId)
            : null;
    }
}
