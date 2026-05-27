namespace DevNexus.Core.Models.Execution;

/// <summary>
/// CLI 执行策略裁决码。
/// </summary>
public enum CliExecutionPolicyDecisionCode
{
    /// <summary>
    /// 允许执行。
    /// </summary>
    Allowed = 0,

    /// <summary>
    /// 工作目录不存在或无法访问。
    /// </summary>
    WorkingDirectoryUnavailable = 1,

    /// <summary>
    /// 命令未在安全命令或允许列表中。
    /// </summary>
    UnsafeCommandRequiresApproval = 2,

    /// <summary>
    /// 命令命中高风险模式。
    /// </summary>
    DangerousCommandRequiresApproval = 3,

    /// <summary>
    /// 保留历史裁决码：命令包含外部绝对路径。
    /// </summary>
    ExternalPathViolation = 4,

    /// <summary>
    /// 命令命中严格内联执行策略。
    /// </summary>
    StrictInlineEvalRequiresApproval = 5,

    /// <summary>
    /// 检测到重复命令循环。
    /// </summary>
    RepeatedCommandLoop = 6,

    /// <summary>
    /// 代码内容为空。
    /// </summary>
    EmptyCodeContent = 7,

    /// <summary>
    /// 代码内容命中高风险模式。
    /// </summary>
    DangerousCodeRequiresApproval = 8
}
