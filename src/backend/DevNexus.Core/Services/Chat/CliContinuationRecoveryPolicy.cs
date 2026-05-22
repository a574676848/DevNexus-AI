using DevNexus.Core.Models.Evaluation;
using DevNexus.Shared.Enums;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// CLI 同会话续接恢复策略。
/// </summary>
internal static class CliContinuationRecoveryPolicy
{
    private const string TerminalInputLabel = "终端输入";
    private const string HostSendCommandInputToolName = "HostService.SendCommandInputAsync";

    /// <summary>
    /// 判断工具记录是否表达 CLI stdin 续接，而不是产品化挂起交互。
    /// </summary>
    public static bool IsInputContinuation(ToolExecutionRecord record)
    {
        return !record.Success
            && !record.RequiresHumanIntervention
            && record.SuggestedAction == ToolSuggestedAction.PromptUserInput
            && (string.Equals(record.RequestedUserInputLabel, TerminalInputLabel, StringComparison.Ordinal) ||
                ContainsToolName(record.UserMessage, HostSendCommandInputToolName) ||
                ContainsToolName(record.ErrorSummary, HostSendCommandInputToolName) ||
                ContainsToolName(record.Output, HostSendCommandInputToolName));
    }

    private static bool ContainsToolName(string? value, string toolName)
    {
        return value?.Contains(toolName, StringComparison.Ordinal) == true;
    }
}
