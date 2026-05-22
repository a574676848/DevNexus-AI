using DevNexus.Core.Models.Execution;
using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Services.Cli;

/// <summary>
/// CLI 续接工具响应格式化器。
/// </summary>
internal static class CliContinuationToolResponseFormatter
{
    private const string WaitToolName = "HostService.WaitCommandAsync";
    private const string SendInputToolName = "HostService.SendCommandInputAsync";
    private const string StopToolName = "HostService.StopCommandAsync";
    private const string ReviewResultAction = "ReviewResult";

    /// <summary>
    /// 将 CLI 会话状态格式化为模型可消费的统一文本协议。
    /// </summary>
    public static string Format(string action, CliExecSessionDto? session)
    {
        if (session?.State == null)
        {
            return TaggedExecutionText.Failure($"{action}失败：未找到终端会话。");
        }

        var state = session.State;
        var summary = state.StatusSummary;
        var lines = new List<string>
        {
            $"{action}：{summary?.Label ?? state.ExecStatus.ToString()}",
            $"sessionId: {session.SessionId}",
            $"status: {state.ExecStatus}",
            $"isActive: {state.IsActive}",
            $"waitingForInput: {state.WaitingForInput}",
            $"nextAction: {summary?.NextAction ?? "ViewDetails"}",
            $"recommendedTool: {ResolveRecommendedTool(state)}"
        };

        if (!string.IsNullOrWhiteSpace(summary?.Description))
        {
            lines.Add($"description: {summary.Description}");
        }

        if (!string.IsNullOrWhiteSpace(session.OutputTail))
        {
            lines.Add("outputTail:");
            lines.Add(session.OutputTail);
        }

        return state.IsActive
            ? TaggedExecutionText.Info(string.Join(Environment.NewLine, lines))
            : TaggedExecutionText.Success(string.Join(Environment.NewLine, lines));
    }

    /// <summary>
    /// 将 CLI 终止结果格式化为模型可消费的统一文本协议。
    /// </summary>
    public static string FormatTermination(string action, CliExecTerminateResultDto result)
    {
        var state = result.State;
        var summary = state?.StatusSummary;
        var lines = new List<string>
        {
            $"{action}：{result.Message}",
            $"sessionId: {result.SessionId}",
            $"terminated: {result.Terminated}",
            $"alreadyExited: {result.AlreadyExited}"
        };

        if (state != null)
        {
            lines.Add($"status: {state.ExecStatus}");
            lines.Add($"isActive: {state.IsActive}");
            lines.Add($"waitingForInput: {state.WaitingForInput}");
            lines.Add($"nextAction: {summary?.NextAction ?? "ReviewResult"}");
            lines.Add($"recommendedTool: {ResolveTerminationRecommendedTool(state)}");
        }
        else
        {
            lines.Add($"nextAction: {ReviewResultAction}");
            lines.Add($"recommendedTool: {ReviewResultAction}");
        }

        if (!string.IsNullOrWhiteSpace(summary?.Description))
        {
            lines.Add($"description: {summary.Description}");
        }

        return result.Terminated || (result.AlreadyExited && state != null)
            ? TaggedExecutionText.Success(string.Join(Environment.NewLine, lines))
            : TaggedExecutionText.Failure(string.Join(Environment.NewLine, lines));
    }

    private static string ResolveRecommendedTool(CliSessionStateDto state)
    {
        if (state.WaitingForInput || string.Equals(state.StatusSummary?.NextAction, "SendInput", StringComparison.Ordinal))
        {
            return SendInputToolName;
        }

        return state.IsActive ? WaitToolName : ReviewResultAction;
    }

    private static string ResolveTerminationRecommendedTool(CliSessionStateDto state)
    {
        return state.IsActive ? StopToolName : ReviewResultAction;
    }
}
