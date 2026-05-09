using DevNexus.Core.Models.Execution;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// 宿主交互的内部结构化服务接口。
/// 仅供后端内部编排使用，不直接暴露给模型工具面。
/// </summary>
public interface IHostStructuredService
{
    /// <summary>
    /// 验证路径是否在允许的工作区范围内。
    /// </summary>
    bool ValidatePathAccess(string path);

    /// <summary>
    /// 结构化执行 Shell 命令。
    /// </summary>
    Task<HostCommandExecutionResult> ExecuteCommandResultAsync(
        string command,
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 结构化读取文本文件。
    /// </summary>
    Task<HostTextOperationResult> ReadFileTextResultAsync(
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 结构化写入文本文件。
    /// </summary>
    Task<HostOperationResult> WriteFileTextResultAsync(
        string path,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 结构化列出目录内容。
    /// </summary>
    Task<HostTextOperationResult> ListDirectoryResultAsync(
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 结构化搜索文件内容。
    /// </summary>
    Task<HostTextOperationResult> SearchInFilesResultAsync(
        string directory,
        string query,
        string filePattern = "*",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 结构化递归列出匹配文件。
    /// </summary>
    Task<HostFileListOperationResult> ListFilesRecursiveResultAsync(
        string path,
        string[] patterns,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 结构化应用差异补丁。
    /// </summary>
    Task<HostOperationResult> ApplyDiffResultAsync(
        string path,
        string originalContent,
        string newContent,
        CancellationToken cancellationToken = default);
}
