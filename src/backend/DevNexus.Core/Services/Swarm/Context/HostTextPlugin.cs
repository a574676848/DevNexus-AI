using System.ComponentModel;
using DevNexus.Core.Abstractions;
using DevNexus.Core.Models.Execution;
using Microsoft.SemanticKernel;

namespace DevNexus.Core.Services.Swarm.Context;

/// <summary>
/// 宿主服务文本插件适配器。
/// 将内部宿主服务以稳定的文本工具面暴露给模型。
/// </summary>
public class HostTextPlugin
{
    private readonly IHostStructuredService _hostService;

    /// <summary>
    /// 初始化宿主服务文本插件适配器。
    /// </summary>
    public HostTextPlugin(IHostStructuredService hostService)
    {
        _hostService = hostService;
    }

    [KernelFunction, Description("读取文本文件内容")]
    public async Task<string> ReadFileTextAsync(
        [Description("文件的绝对路径")] string path,
        CancellationToken cancellationToken = default)
    {
        return HostOperationTextFormatter.FormatText(
            await _hostService.ReadFileTextResultAsync(path, cancellationToken));
    }

    [KernelFunction, Description("将内容写入文本文件")]
    public async Task<string> WriteFileTextAsync(
        [Description("文件的绝对路径")] string path,
        [Description("要写入的文本内容")] string content,
        CancellationToken cancellationToken = default)
    {
        return HostOperationTextFormatter.Format(
            await _hostService.WriteFileTextResultAsync(path, content, cancellationToken));
    }

    [KernelFunction, Description("递归列出匹配模式的文件。建议优先使用 ListDirectoryAsync 进行快速概览。")]
    public async Task<object> ListFilesRecursiveAsync(
        [Description("根目录路径")] string path,
        [Description("搜索模式（如 *.cs、*.md）")] string[] patterns,
        CancellationToken cancellationToken = default)
    {
        return HostOperationTextFormatter.FormatFileList(
            await _hostService.ListFilesRecursiveResultAsync(path, patterns, cancellationToken));
    }

    [KernelFunction, Description("在文件中搜索字符串（基于 grep 逻辑）")]
    public async Task<string> SearchInFilesAsync(
        [Description("搜索根目录")] string directory,
        [Description("关键词或正则表达式")] string query,
        [Description("文件过滤模式")] string filePattern = "*",
        CancellationToken cancellationToken = default)
    {
        return HostOperationTextFormatter.FormatText(
            await _hostService.SearchInFilesResultAsync(directory, query, filePattern, cancellationToken),
            preferTextOnSuccess: false);
    }

    [KernelFunction, Description("列出目录下的文件和子目录（非递归，带详细信息）")]
    public async Task<string> ListDirectoryAsync(
        [Description("目标目录路径")] string path,
        CancellationToken cancellationToken = default)
    {
        return HostOperationTextFormatter.FormatText(
            await _hostService.ListDirectoryResultAsync(path, cancellationToken));
    }

    [KernelFunction, Description("应用代码差异补丁")]
    public async Task<string> ApplyDiffAsync(
        [Description("目标文件路径")] string path,
        [Description("期望匹配的原始证据内容")] string originalContent,
        [Description("替换后的新内容")] string newContent,
        CancellationToken cancellationToken = default)
    {
        return HostOperationTextFormatter.Format(
            await _hostService.ApplyDiffResultAsync(path, originalContent, newContent, cancellationToken));
    }

    [KernelFunction, Description("在指定工作目录中执行 Shell 命令")]
    public async Task<string> ExecuteCommandAsync(
        [Description("要执行的完整命令字符串")] string command,
        [Description("命令参数")] string arguments,
        [Description("工作目录路径")] string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        return HostOperationTextFormatter.FormatCommand(
            await _hostService.ExecuteCommandResultAsync(command, arguments, workingDirectory, cancellationToken));
    }
}
