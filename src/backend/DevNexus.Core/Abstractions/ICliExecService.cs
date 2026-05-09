using DevNexus.Core.Models.Execution;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// CLI 执行入口服务。
/// 负责接受一次命令请求，并完成策略、审批与执行接入。
/// </summary>
public interface ICliExecService
{
    /// <summary>
    /// 提交一次 CLI 执行请求。
    /// </summary>
    Task<HostCommandExecutionResult> ExecuteCommandResultAsync(
        string command,
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken = default);
}
