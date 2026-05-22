using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Cli;

/// <summary>
/// CLI 运行时状态摘要构建器。
/// </summary>
public static class CliRuntimeStatusSummaryBuilder
{
    private const string NeutralTone = "neutral";
    private const string ActiveTone = "active";
    private const string WaitingTone = "waiting";
    private const string SuccessTone = "success";
    private const string WarningTone = "warning";
    private const string DangerTone = "danger";

    private const string SendInputAction = "SendInput";
    private const string WatchOutputAction = "WatchOutput";
    private const string ReviewResultAction = "ReviewResult";
    private const string RetryAction = "Retry";
    private const string RollbackAction = "Rollback";
    private const string ViewDetailsAction = "ViewDetails";

    /// <summary>
    /// 根据 CLI 状态构建低噪运行时摘要。
    /// </summary>
    public static CliRuntimeStatusSummaryDto Build(
        CliExecStatus execStatus,
        bool waitingForInput,
        string? terminationReason)
    {
        if (waitingForInput || execStatus == CliExecStatus.WaitingForInput)
        {
            return Create(
                WaitingTone,
                "等待输入",
                "终端等待输入，底部输入框会直接发送。",
                SendInputAction,
                requiresInput: true,
                isTerminal: false,
                terminationReason);
        }

        return execStatus switch
        {
            CliExecStatus.Requested or CliExecStatus.Queued or CliExecStatus.PendingApproval => Create(
                NeutralTone,
                execStatus == CliExecStatus.PendingApproval ? "等待审批" : "等待执行",
                execStatus == CliExecStatus.PendingApproval ? "终端命令等待确认后继续。" : "终端命令已进入执行队列。",
                ViewDetailsAction,
                requiresInput: false,
                isTerminal: false,
                terminationReason),
            CliExecStatus.Running => Create(
                ActiveTone,
                "运行中",
                "终端仍在运行，可随时查看。",
                WatchOutputAction,
                requiresInput: false,
                isTerminal: false,
                terminationReason),
            CliExecStatus.Completed => Create(
                SuccessTone,
                "已完成",
                "终端命令已完成，可查看输出结果。",
                ReviewResultAction,
                requiresInput: false,
                isTerminal: true,
                terminationReason),
            CliExecStatus.RolledBack => Create(
                SuccessTone,
                "已回滚",
                "终端变更已回滚，可继续下一步操作。",
                ReviewResultAction,
                requiresInput: false,
                isTerminal: true,
                terminationReason),
            CliExecStatus.Cancelled => Create(
                WarningTone,
                "已停止",
                "终端会话已停止，可检查输出后决定是否重试。",
                RetryAction,
                requiresInput: false,
                isTerminal: true,
                terminationReason),
            CliExecStatus.TimedOut => Create(
                WarningTone,
                "已超时",
                "终端执行超时，建议查看输出并缩小命令范围后重试。",
                RetryAction,
                requiresInput: false,
                isTerminal: true,
                terminationReason),
            CliExecStatus.Reaped => Create(
                WarningTone,
                "已结束",
                "终端会话已被运行时回收，可查看归档输出。",
                ViewDetailsAction,
                requiresInput: false,
                isTerminal: true,
                terminationReason),
            CliExecStatus.Failed => Create(
                DangerTone,
                "失败",
                "终端命令执行失败，建议先查看失败输出和工作目录状态。",
                RollbackAction,
                requiresInput: false,
                isTerminal: true,
                terminationReason),
            _ => Create(
                NeutralTone,
                "未知",
                "查看终端详情。",
                ViewDetailsAction,
                requiresInput: false,
                isTerminal: false,
                terminationReason)
        };
    }

    private static CliRuntimeStatusSummaryDto Create(
        string tone,
        string label,
        string description,
        string nextAction,
        bool requiresInput,
        bool isTerminal,
        string? terminationReason)
    {
        return new CliRuntimeStatusSummaryDto
        {
            Tone = tone,
            Label = label,
            Description = description,
            NextAction = nextAction,
            RequiresInput = requiresInput,
            IsTerminal = isTerminal,
            TerminationReasonText = CliSessionTerminationReasons.GetDisplayText(terminationReason)
        };
    }
}
