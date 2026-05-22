using DevNexus.Shared.Enums;

namespace DevNexus.Core.Models.Execution;

/// <summary>
/// CLI 执行策略评估结果。
/// </summary>
public sealed record CliExecutionPolicyResult
{
    /// <summary>
    /// 是否允许继续执行。
    /// </summary>
    public bool Allowed { get; init; } = true;

    /// <summary>
    /// 有效工作目录。
    /// </summary>
    public string? EffectiveWorkingDirectory { get; init; }

    /// <summary>
    /// 策略裁决码。
    /// </summary>
    public CliExecutionPolicyDecisionCode DecisionCode { get; init; } = CliExecutionPolicyDecisionCode.Allowed;

    /// <summary>
    /// 失败原因。
    /// </summary>
    public ToolFailureReason FailureReason { get; init; } = ToolFailureReason.None;

    /// <summary>
    /// 建议动作。
    /// </summary>
    public ToolSuggestedAction SuggestedAction { get; init; } = ToolSuggestedAction.None;

    /// <summary>
    /// 结果文案。
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 是否需要人工介入。
    /// </summary>
    public bool RequiresHumanIntervention { get; init; }

    /// <summary>
    /// 命令指纹。
    /// </summary>
    public string? CommandFingerprint { get; init; }

    /// <summary>
    /// 命令模式。
    /// </summary>
    public string? CommandPattern { get; init; }

    /// <summary>
    /// 创建允许结果。
    /// </summary>
    public static CliExecutionPolicyResult Allow(string effectiveWorkingDirectory)
    {
        return new CliExecutionPolicyResult
        {
            Allowed = true,
            EffectiveWorkingDirectory = effectiveWorkingDirectory
        };
    }

    /// <summary>
    /// 创建拒绝结果。
    /// </summary>
    public static CliExecutionPolicyResult Block(
        CliExecutionPolicyDecisionCode decisionCode,
        string message,
        ToolFailureReason failureReason,
        ToolSuggestedAction suggestedAction,
        bool requiresHumanIntervention = false,
        string? commandFingerprint = null,
        string? commandPattern = null)
    {
        return new CliExecutionPolicyResult
        {
            Allowed = false,
            DecisionCode = decisionCode,
            Message = message,
            FailureReason = failureReason,
            SuggestedAction = suggestedAction,
            RequiresHumanIntervention = requiresHumanIntervention,
            CommandFingerprint = commandFingerprint,
            CommandPattern = commandPattern
        };
    }
}
