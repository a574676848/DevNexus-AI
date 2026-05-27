using DevNexus.Core.Models.Execution;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// CLI 执行策略服务。
/// 负责工作目录解析、危险命令识别、内联执行拦截和重复命令保护。
/// </summary>
public interface ICliExecutionPolicyService
{
    /// <summary>
    /// 解析本机工作目录。
    /// </summary>
    string ResolveWorkingDirectory(Guid userId, string? requestedWorkingDirectory);

    /// <summary>
    /// 评估命令执行请求是否允许继续。
    /// </summary>
    Task<CliExecutionPolicyResult> EvaluateCommandAsync(
        Guid userId,
        string sessionId,
        string command,
        string arguments,
        string workingDirectory,
        AgentApprovalMode approvalMode = AgentApprovalMode.AskUser,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 评估代码内容是否允许继续执行。
    /// </summary>
    CliExecutionPolicyResult EvaluateCodeContent(
        string language,
        string code,
        AgentApprovalMode approvalMode = AgentApprovalMode.AskUser);
}
