using DevNexus.Core.Models.Evaluation;
using DevNexus.Core.Models.Execution;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 工具执行结果分类器。
/// 用于将文本输出映射为结构化失败原因和建议动作。
/// </summary>
public static class ToolExecutionResultClassifier
{
    /// <summary>
    /// 对工具执行输出进行分类。
    /// </summary>
    public static ToolExecutionClassificationResult Classify(string output, bool requireTaggedOutput = false)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return requireTaggedOutput ? CreateMissingTagFailure() : ToolExecutionClassificationResult.CreateSuccess();
        }

        var parsedOutput = TaggedExecutionOutput.Parse(output);
        var normalized = parsedOutput.Raw.Trim();
        var lowered = normalized.ToLowerInvariant();

        if (requireTaggedOutput && !parsedOutput.HasExplicitTag)
        {
            return CreateMissingTagFailure();
        }

        if (IsStillRunningOutput(parsedOutput, lowered))
        {
            return ToolExecutionClassificationResult.CreateFailure(
                ToolFailureReason.None,
                retryable: true,
                requiresHumanIntervention: false,
                shouldFallback: false,
                shouldRotateCredential: false,
                suggestedAction: ToolSuggestedAction.WaitForCompletion,
                userMessage: "终端命令仍在运行，应等待同一会话结束或查看最新输出，不要重新启动相同命令。");
        }

        if (IsWaitingForInputOutput(parsedOutput, lowered))
        {
            return ToolExecutionClassificationResult.CreateFailure(
                ToolFailureReason.MissingUserInput,
                retryable: false,
                requiresHumanIntervention: false,
                shouldFallback: false,
                shouldRotateCredential: false,
                suggestedAction: ToolSuggestedAction.PromptUserInput,
                userMessage: "终端正在等待输入，应通过 HostService.SendCommandInputAsync 向同一会话发送 stdin。",
                requestedUserInputKind: "text",
                requestedUserInputLabel: "终端输入");
        }

        if (IsStopCommandOutput(parsedOutput, lowered))
        {
            return ToolExecutionClassificationResult.CreateFailure(
                ToolFailureReason.None,
                retryable: true,
                requiresHumanIntervention: false,
                shouldFallback: false,
                shouldRotateCredential: false,
                suggestedAction: ToolSuggestedAction.StopCommand,
                userMessage: "当前终端会话仍处于活跃状态，应继续调用 HostService.StopCommandAsync 停止同一会话，不要重新启动命令或降级到其他工具。");
        }

        if (IsSuccessOutput(parsedOutput, lowered))
        {
            return ToolExecutionClassificationResult.CreateSuccess();
        }

        if (parsedOutput.Status == TaggedExecutionStatus.SecurityBlocked)
        {
            return ToolExecutionClassificationResult.CreateFailure(
                ToolFailureReason.ApprovalRequired,
                retryable: false,
                requiresHumanIntervention: true,
                shouldFallback: false,
                shouldRotateCredential: false,
                suggestedAction: ToolSuggestedAction.RequestApproval,
                userMessage: "当前操作被安全策略拦截，不能继续自动执行。");
        }

        if (ContainsAny(lowered, "401", "unauthorized", "未授权", "登录已过期", "token expired", "refresh token"))
        {
            return ToolExecutionClassificationResult.CreateFailure(
                ToolFailureReason.AuthExpired,
                retryable: false,
                requiresHumanIntervention: false,
                shouldFallback: true,
                shouldRotateCredential: true,
                suggestedAction: ToolSuggestedAction.RefreshCredential,
                userMessage: "认证已失效，优先刷新登录态或切换可用凭证。");
        }

        if (ContainsAny(lowered, "password", "密码", "请提供", "请补充", "需要重新登录", "prompt-missing"))
        {
            return ToolExecutionClassificationResult.CreateFailure(
                ToolFailureReason.MissingUserInput,
                retryable: false,
                requiresHumanIntervention: true,
                shouldFallback: false,
                shouldRotateCredential: false,
                suggestedAction: ToolSuggestedAction.PromptUserInput,
                userMessage: "当前执行缺少用户提供的前置条件，应暂停自动执行并等待用户补充信息。",
                requestedUserInputKind: "text",
                requestedUserInputLabel: "必要输入");
        }

        if (ContainsAny(lowered, "[security_blocked]", "approval", "需要审批", "用户拒绝", "denied by user"))
        {
            return ToolExecutionClassificationResult.CreateFailure(
                ToolFailureReason.ApprovalRequired,
                retryable: false,
                requiresHumanIntervention: true,
                shouldFallback: false,
                shouldRotateCredential: false,
                suggestedAction: ToolSuggestedAction.RequestApproval,
                userMessage: "当前操作需要人工审批或已被用户拒绝，不能继续自动执行。");
        }

        if (ContainsAny(lowered, "path access", "permission denied", "权限拒绝", "路径访问被拒绝", "access denied"))
        {
            return ToolExecutionClassificationResult.CreateFailure(
                ToolFailureReason.PermissionDenied,
                retryable: false,
                requiresHumanIntervention: false,
                shouldFallback: true,
                shouldRotateCredential: false,
                suggestedAction: ToolSuggestedAction.Fallback,
                userMessage: "当前路径或资源访问被拒绝，应切换到允许的工作区或改用受控路径。");
        }

        if (ContainsAny(lowered, "could not find file", "could not find a part of the path", "文件不存在", "路径不存在", "目录不存在"))
        {
            return ToolExecutionClassificationResult.CreateFailure(
                ToolFailureReason.PathNotFound,
                retryable: false,
                requiresHumanIntervention: false,
                shouldFallback: true,
                shouldRotateCredential: false,
                suggestedAction: ToolSuggestedAction.Fallback,
                userMessage: "目标文件或目录不存在，应先验证路径来源，再决定是否切换到其他执行方案。");
        }

        if (ContainsAny(lowered, "429", "rate limit", "too many requests", "限流"))
        {
            return ToolExecutionClassificationResult.CreateFailure(
                ToolFailureReason.RateLimited,
                retryable: true,
                requiresHumanIntervention: false,
                shouldFallback: true,
                shouldRotateCredential: false,
                suggestedAction: ToolSuggestedAction.Retry,
                userMessage: "当前请求触发限流，可等待或切换备用路径后重试。");
        }

        if (ContainsAny(lowered, "insufficient_quota", "payment required", "额度", "配额", "billing"))
        {
            return ToolExecutionClassificationResult.CreateFailure(
                ToolFailureReason.BillingLimited,
                retryable: false,
                requiresHumanIntervention: true,
                shouldFallback: true,
                shouldRotateCredential: false,
                suggestedAction: ToolSuggestedAction.Fallback,
                userMessage: "当前额度或账单受限，应切换备用 Provider 或等待人工处理。");
        }

        if (ContainsAny(lowered, "context length", "maximum context", "上下文过长", "max_tokens", "token limit"))
        {
            return ToolExecutionClassificationResult.CreateFailure(
                ToolFailureReason.ContextOverflow,
                retryable: true,
                requiresHumanIntervention: false,
                shouldFallback: true,
                shouldRotateCredential: false,
                suggestedAction: ToolSuggestedAction.Fallback,
                userMessage: "上下文或返回内容过长，应压缩上下文或改用更轻量的执行路径。");
        }

        if (ContainsAny(lowered, "timeout", "timed out", "连接失败", "connection reset", "network"))
        {
            return ToolExecutionClassificationResult.CreateFailure(
                ToolFailureReason.TransientNetworkFailure,
                retryable: true,
                requiresHumanIntervention: false,
                shouldFallback: true,
                shouldRotateCredential: false,
                suggestedAction: ToolSuggestedAction.Retry,
                userMessage: "检测到临时网络或超时问题，可以重试或切换备用方案。");
        }

        if (parsedOutput.IsFailureLike)
        {
            return ToolExecutionClassificationResult.CreateFailure(
                ToolFailureReason.FatalExecutionError,
                retryable: false,
                requiresHumanIntervention: false,
                shouldFallback: true,
                shouldRotateCredential: false,
                suggestedAction: ToolSuggestedAction.Fallback,
                userMessage: "当前工具执行失败，需要改用其他可行路径或重新规划执行策略。");
        }

        return ToolExecutionClassificationResult.CreateFailure(
            ToolFailureReason.Unknown,
            retryable: false,
            requiresHumanIntervention: false,
            shouldFallback: true,
            shouldRotateCredential: false,
            suggestedAction: ToolSuggestedAction.Fallback,
            userMessage: "出现未分类的工具失败，应结合诊断信息重新规划执行路径。");
    }

    private static ToolExecutionClassificationResult CreateMissingTagFailure()
    {
        return ToolExecutionClassificationResult.CreateFailure(
            ToolFailureReason.ToolFormatError,
            retryable: true,
            requiresHumanIntervention: false,
            shouldFallback: false,
            shouldRotateCredential: false,
            suggestedAction: ToolSuggestedAction.Retry,
            userMessage: "受控工具输出缺少统一结果标签，应返回 [SUCCESS]、[FAILURE]、[INFO]、[EXCEPTION] 或 [SECURITY_BLOCKED] 后再继续。");
    }

    private static bool IsSuccessOutput(TaggedExecutionOutput parsedOutput, string lowered)
    {
        if (parsedOutput.IsExplicitSuccess)
        {
            return true;
        }

        return !parsedOutput.IsFailureLike
               && !ContainsAny(lowered, "error", "exception", "失败", "错误");
    }

    private static bool IsStillRunningOutput(TaggedExecutionOutput parsedOutput, string lowered)
    {
        return parsedOutput.Status == TaggedExecutionStatus.Info
               && ContainsAny(
                   lowered,
                   "[stillrunning]",
                   "命令仍在运行",
                   "终端命令仍在后台运行",
                   "recommendedtool: hostservice.waitcommandasync");
    }

    private static bool IsWaitingForInputOutput(TaggedExecutionOutput parsedOutput, string lowered)
    {
        return parsedOutput.Status == TaggedExecutionStatus.Info
               && ContainsAny(
                   lowered,
                   "waitingforinput: true",
                   "nextaction: sendinput",
                   "recommendedtool: hostservice.sendcommandinputasync",
                   "等待输入");
    }

    private static bool IsStopCommandOutput(TaggedExecutionOutput parsedOutput, string lowered)
    {
        return parsedOutput.IsFailureLike
               && ContainsAny(
                   lowered,
                   "recommendedtool: hostservice.stopcommandasync");
    }

    private static bool ContainsAny(string source, params string[] markers)
    {
        return markers.Any(marker => source.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// 工具执行结果分类结果。
/// </summary>
public sealed record ToolExecutionClassificationResult
{
    /// <summary>
    /// 是否成功。
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 失败原因。
    /// </summary>
    public ToolFailureReason FailureReason { get; init; } = ToolFailureReason.None;

    /// <summary>
    /// 是否可重试。
    /// </summary>
    public bool Retryable { get; init; }

    /// <summary>
    /// 是否需要人工介入。
    /// </summary>
    public bool RequiresHumanIntervention { get; init; }

    /// <summary>
    /// 是否建议走备用路线。
    /// </summary>
    public bool ShouldFallback { get; init; }

    /// <summary>
    /// 是否建议轮换或刷新凭证。
    /// </summary>
    public bool ShouldRotateCredential { get; init; }

    /// <summary>
    /// 建议动作。
    /// </summary>
    public ToolSuggestedAction SuggestedAction { get; init; } = ToolSuggestedAction.None;

    /// <summary>
    /// 用户提示文案。
    /// </summary>
    public string? UserMessage { get; init; }

    /// <summary>
    /// 请求输入类型。
    /// </summary>
    public string? RequestedUserInputKind { get; init; }

    /// <summary>
    /// 请求输入标题。
    /// </summary>
    public string? RequestedUserInputLabel { get; init; }

    /// <summary>
    /// 构造成功结果。
    /// </summary>
    public static ToolExecutionClassificationResult CreateSuccess()
    {
        return new ToolExecutionClassificationResult
        {
            Success = true,
            FailureReason = ToolFailureReason.None,
            SuggestedAction = ToolSuggestedAction.None
        };
    }

    /// <summary>
    /// 构造失败结果。
    /// </summary>
    public static ToolExecutionClassificationResult CreateFailure(
        ToolFailureReason failureReason,
        bool retryable,
        bool requiresHumanIntervention,
        bool shouldFallback,
        bool shouldRotateCredential,
        ToolSuggestedAction suggestedAction,
        string userMessage,
        string? requestedUserInputKind = null,
        string? requestedUserInputLabel = null)
    {
        return new ToolExecutionClassificationResult
        {
            Success = false,
            FailureReason = failureReason,
            Retryable = retryable,
            RequiresHumanIntervention = requiresHumanIntervention,
            ShouldFallback = shouldFallback,
            ShouldRotateCredential = shouldRotateCredential,
            SuggestedAction = suggestedAction,
            UserMessage = userMessage,
            RequestedUserInputKind = requestedUserInputKind,
            RequestedUserInputLabel = requestedUserInputLabel
        };
    }
}
