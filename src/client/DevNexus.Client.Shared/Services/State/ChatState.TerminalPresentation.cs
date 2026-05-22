using DevNexus.Client.Shared.Helpers;
using DevNexus.Client.Shared.Models;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Client.Shared.Services.State;

public partial class ChatState
{
    public TerminalPresentationState? GetTerminalPresentation(Guid sessionId)
    {
        if (sessionId == Guid.Empty)
        {
            return null;
        }

        var primaryRecord = GetFocusedTerminalRecord(sessionId)
            ?? GetTerminalRecords(sessionId).FirstOrDefault();
        var cliExecSession = GetCliExecSession(sessionId);
        var runState = ResolveSessionRunState(sessionId);

        if (primaryRecord == null && (cliExecSession == null || !cliExecSession.IsActive))
        {
            return null;
        }

        var waitingForInput = cliExecSession?.WaitingForInput == true || primaryRecord?.WaitingForInput == true;
        var statusLabel = ResolveTerminalStatusLabel(sessionId, runState, primaryRecord, waitingForInput, cliExecSession?.StatusSummary);
        var description = ResolveTerminalDescription(runState, waitingForInput, primaryRecord, cliExecSession?.StatusSummary);
        var metaLine = ResolveTerminalMetaLine(primaryRecord, cliExecSession);
        var toneClass = ResolveTerminalToneClass(primaryRecord, waitingForInput, cliExecSession?.StatusSummary);
        var workingDirectory = cliExecSession?.WorkingDirectory ?? primaryRecord?.WorkingDirectory;
        var command = string.IsNullOrWhiteSpace(cliExecSession?.Command)
            ? primaryRecord?.Command
            : cliExecSession?.Command;
        var watchSummary = primaryRecord?.WatchSummary;

        return new TerminalPresentationState
        {
            SessionId = sessionId,
            RecordId = primaryRecord?.RecordId,
            Headline = cliExecSession?.IsActive == true ? "聊天是唯一执行入口" : "终端",
            StatusLabel = statusLabel,
            Description = description,
            MetaLine = metaLine,
            ToneClass = toneClass,
            WaitingForInput = waitingForInput,
            IsActive = cliExecSession?.IsActive == true || primaryRecord?.IsActive == true,
            RunStateLabel = GetSessionRunStatusText(sessionId),
            WorkingDirectory = workingDirectory,
            Command = command,
            WatchSummary = watchSummary,
            ScopeLabel = $"会话 {FormatCompactSessionId(sessionId)}",
            ModeLabel = "聊天执行"
        };
    }

    private string ResolveTerminalStatusLabel(
        Guid sessionId,
        ChatSessionRunState runState,
        TerminalRecordState? primaryRecord,
        bool waitingForInput,
        CliRuntimeStatusSummaryDto? summary)
    {
        if (summary != null)
        {
            return summary.Label;
        }

        if (runState is ChatSessionRunState.WaitingForInput
            or ChatSessionRunState.Running
            or ChatSessionRunState.Recovering
            or ChatSessionRunState.WaitingForPendingInput
            or ChatSessionRunState.WaitingForApproval)
        {
            return GetSessionRunStatusText(sessionId);
        }

        if (primaryRecord == null)
        {
            return "未知";
        }

        if (waitingForInput)
        {
            return "等待输入";
        }

        return TerminalDisplayHelper.FormatSessionState(primaryRecord.SessionState);
    }

    private static string ResolveTerminalDescription(
        ChatSessionRunState runState,
        bool waitingForInput,
        TerminalRecordState? primaryRecord,
        CliRuntimeStatusSummaryDto? summary)
    {
        if (summary != null)
        {
            return summary.Description;
        }

        if (waitingForInput)
        {
            return "终端等待输入，底部输入框会直接发送。";
        }

        if (runState == ChatSessionRunState.Recovering)
        {
            return "正在同步终端状态，可稍后查看最新结果。";
        }

        if (primaryRecord?.IsActive == true)
        {
            return "终端仍在运行，可随时查看。";
        }

        return "查看终端详情";
    }

    private static string ResolveTerminalMetaLine(
        TerminalRecordState? primaryRecord,
        CliSessionStateDto? cliExecSession)
    {
        var parts = new List<string>();
        var command = string.IsNullOrWhiteSpace(cliExecSession?.Command)
            ? primaryRecord?.Command
            : cliExecSession?.Command;
        var workingDirectory = cliExecSession?.WorkingDirectory ?? primaryRecord?.WorkingDirectory;

        if (!string.IsNullOrWhiteSpace(command))
        {
            parts.Add(command);
        }

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            parts.Add(Path.GetFileName(workingDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
        }

        var latest = TerminalDisplayHelper.FormatRelativeTime(
            cliExecSession?.LastActivityAt
            ?? primaryRecord?.LastActivityAt
            ?? cliExecSession?.StartedAt
            ?? primaryRecord?.StartedAt);
        if (!string.IsNullOrWhiteSpace(latest))
        {
            parts.Add($"活跃于 {latest}");
        }

        if (!string.IsNullOrWhiteSpace(primaryRecord?.WatchSummary))
        {
            parts.Add(primaryRecord.WatchSummary);
        }

        return parts.Count > 0 ? string.Join(" · ", parts) : "查看终端详情";
    }

    private static string ResolveTerminalToneClass(
        TerminalRecordState? primaryRecord,
        bool waitingForInput,
        CliRuntimeStatusSummaryDto? summary)
    {
        if (summary != null)
        {
            return $"terminal-summary-card--{summary.Tone}";
        }

        if (primaryRecord == null)
        {
            return "terminal-summary-card--neutral";
        }

        return TerminalDisplayHelper.GetSessionToneClass(primaryRecord.SessionState, waitingForInput);
    }

    private static string FormatCompactSessionId(Guid sessionId)
    {
        return sessionId == Guid.Empty ? "unknown" : sessionId.ToString("N")[..8];
    }
}
